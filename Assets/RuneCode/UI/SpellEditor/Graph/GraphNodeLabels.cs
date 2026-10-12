using System.Collections.Generic;
using System.Globalization;
using System.Text;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    /// <summary>
    /// 그래프 노드마다 이름·RAM·핵심 설정 요약·포트 이름을 담은 TMP 라벨을 만들고 배치한다.
    /// 글꼴 크기는 고정하고 Transform 스케일로 확대해 rich text 크기·위치 태그가 노드와 같은 비율을 유지한다.
    /// </summary>
    internal sealed class GraphNodeLabels
    {
        private const float LABEL_FONT_SIZE = 12f;
        private const float LABEL_PADDING_X = 24f;
        private const float LABEL_PADDING_RIGHT = 14f;
        private const float LABEL_PADDING_TOP = 12f;
        private const float HEADER_HEIGHT = 28f;
        private const float PORT_FONT_SIZE = 10f;
        private const float PORT_LABEL_HEIGHT = 16f;
        private const float LABEL_WIDTH = GraphNodeLayout.NODE_WIDTH - LABEL_PADDING_X - LABEL_PADDING_RIGHT;

        private readonly RectTransform _parent;
        private readonly TMP_FontAsset _font;
        private readonly GraphViewport _viewport;
        private readonly GraphNodeLayout _layout;
        private readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
        private readonly Dictionary<string, List<TextMeshProUGUI>> _portLabels = new Dictionary<string, List<TextMeshProUGUI>>();
        private readonly HashSet<string> _existingNodes = new HashSet<string>();
        private readonly List<string> _staleIds = new List<string>();
        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>라벨을 둘 캔버스, 글꼴, 뷰포트와 노드 배치로 라벨 관리자를 만든다.</summary>
        public GraphNodeLabels(RectTransform parent, TMP_FontAsset font, GraphViewport viewport, GraphNodeLayout layout)
        {
            _parent = parent;
            _font = font;
            _viewport = viewport;
            _layout = layout;
        }

        /// <summary>
        /// 그래프 노드마다 라벨 내용을 갱신하고 사라진 노드의 라벨을 지운다. 프리셋 호출 요약은 보관함에서 이름을 찾는다.
        /// 남아 있는 노드 ID 집합을 반환한다(선택 정리용).
        /// </summary>
        public HashSet<string> Refresh(SpellGraph graph, IEnumerable<SpellGraph> library)
        {
            _existingNodes.Clear();
            foreach (GraphNode node in graph.Nodes)
            {
                _existingNodes.Add(node.Id);
                TextMeshProUGUI label = GetOrCreate(node.Id);
                string text = BuildText(node, library);
                if (label.text != text) label.text = text;
                RefreshPortLabels(node);
            }
            RemoveStale();
            return _existingNodes;
        }

        /// <summary>화면 이동·확대 배율에 맞춰 라벨 위치·크기·스케일만 갱신한다.</summary>
        public void Layout(SpellGraph graph)
        {
            float zoom = _viewport.Zoom;
            Vector3 scale = new Vector3(zoom, zoom, 1);
            foreach (GraphNode node in graph.Nodes)
            {
                if (!_labels.TryGetValue(node.Id, out TextMeshProUGUI label)) continue;
                RuneDefinition rune = GameData.Runes.Get(node.RuneId);
                Rect rect = _layout.NodeRect(node, rune);
                RectTransform textRect = label.rectTransform;
                textRect.anchoredPosition = new Vector2(rect.x + LABEL_PADDING_X * zoom, rect.yMax - _parent.rect.yMax - LABEL_PADDING_TOP * zoom);
                textRect.sizeDelta = new Vector2(LABEL_WIDTH, HEADER_HEIGHT);
                textRect.localScale = scale;
                List<TextMeshProUGUI> ports = _portLabels[node.Id];
                int rows = _layout.PortRowCount(node, rune);
                for (int row = 0; row < rows; row++)
                {
                    PortDefinition port = _layout.PortAt(node, rune, SpellGrammar.DIRECTION_IN, row)
                        ?? _layout.PortAt(node, rune, SpellGrammar.DIRECTION_OUT, row);
                    Vector2 point = _layout.PortPosition(rect, node, rune, port.Id);
                    RectTransform portRect = ports[row].rectTransform;
                    portRect.anchoredPosition = new Vector2(rect.x + LABEL_PADDING_X * zoom, point.y - _parent.rect.yMax);
                    portRect.sizeDelta = new Vector2(LABEL_WIDTH, PORT_LABEL_HEIGHT);
                    portRect.localScale = scale;
                }
            }
        }

        /// <summary>노드 ID의 라벨을 반환하며 없으면 고정 글꼴 크기와 좌상단 기준점으로 새로 만든다.</summary>
        private TextMeshProUGUI GetOrCreate(string nodeId)
        {
            if (_labels.TryGetValue(nodeId, out TextMeshProUGUI label)) return label;
            label = CreateLabel(LABEL_FONT_SIZE, false);
            _labels.Add(nodeId, label);
            _portLabels.Add(nodeId, new List<TextMeshProUGUI>());
            return label;
        }

        /// <summary>프리팹으로 라벨을 만들고 글꼴 크기·세로 정렬·좌상단 기준점을 지정한다.</summary>
        private TextMeshProUGUI CreateLabel(float fontSize, bool isPort)
        {
            TextMeshProUGUI label = Object.Instantiate(Resources.Load<TextMeshProUGUI>("RuneCode/UI/Elements/GraphNodeLabel"), _parent, false);
            label.font = _font;
            label.fontSize = fontSize;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.horizontalAlignment = HorizontalAlignmentOptions.Left;
            label.verticalAlignment = isPort ? VerticalAlignmentOptions.Middle : VerticalAlignmentOptions.Top;
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, isPort ? .5f : 1);
            return label;
        }

        /// <summary>표시 포트 행의 라벨을 재사용해 입력·출력 이름을 갱신하고 사라진 행은 숨긴다.</summary>
        private void RefreshPortLabels(GraphNode node)
        {
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            List<TextMeshProUGUI> labels = _portLabels[node.Id];
            int rows = _layout.PortRowCount(node, rune);
            for (int row = 0; row < rows; row++)
            {
                if (row == labels.Count) labels.Add(CreateLabel(PORT_FONT_SIZE, true));
                TextMeshProUGUI label = labels[row];
                label.gameObject.SetActive(true);
                string text = BuildPortText(node, rune, row, label);
                if (label.text != text) label.text = text;
            }
            for (int row = rows; row < labels.Count; row++) labels[row].gameObject.SetActive(false);
        }

        /// <summary>그래프에서 사라진 노드의 라벨 오브젝트를 파괴하고 목록에서 뺀다.</summary>
        private void RemoveStale()
        {
            _staleIds.Clear();
            foreach (var pair in _labels)
            {
                if (!_existingNodes.Contains(pair.Key)) _staleIds.Add(pair.Key);
            }
            foreach (string id in _staleIds)
            {
                Object.Destroy(_labels[id].gameObject);
                _labels.Remove(id);
                foreach (TextMeshProUGUI label in _portLabels[id]) Object.Destroy(label.gameObject);
                _portLabels.Remove(id);
            }
        }

        /// <summary>
        /// 룬 이름·RAM과 핵심 설정 요약을 담은 제목 라벨 rich text를 반환한다. 포트 이름은 별도 행 라벨로 표시한다.
        /// </summary>
        private string BuildText(GraphNode node, IEnumerable<SpellGraph> library)
        {
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            _builder.Clear();
            _builder.Append("<b>").Append(rune.Name).Append("</b>  ").Append(GameData.Runes.NodeRam(node)).Append(" RAM");
            string summary = Summary(node, rune, library);
            if (!string.IsNullOrEmpty(summary)) _builder.Append("\n<size=10>").Append(summary).Append("</size>");
            return _builder.ToString();
        }

        /// <summary>지정 포트 행의 입력 이름과 오른쪽 정렬한 출력 이름을 반환하며 지원 중단 출력에는 취소선을 붙인다.</summary>
        private string BuildPortText(GraphNode node, RuneDefinition rune, int row, TextMeshProUGUI label)
        {
            _builder.Clear();
            PortDefinition input = _layout.PortAt(node, rune, SpellGrammar.DIRECTION_IN, row);
            PortDefinition output = _layout.PortAt(node, rune, SpellGrammar.DIRECTION_OUT, row);
            _builder.Append(input?.Id);
            if (output != null)
            {
                string outputText = output.IsDeprecated ? "<s>" + output.Id + "</s>" : output.Id;
                float outputWidth = label.GetPreferredValues(outputText).x;
                float outputStart = Mathf.Max(0, LABEL_WIDTH - outputWidth);
                _builder.Append("<pos=").Append(outputStart.ToString("0.#", CultureInfo.InvariantCulture)).Append('>').Append(outputText);
            }
            return _builder.ToString();
        }

        /// <summary>Trigger·Apply·프리셋 호출의 핵심 값(시작 조건, 속성, 호출 대상)을 반환하며 해당 없으면 null을 반환한다. Shape 속성은 붙은 속성 노드로 보인다.</summary>
        private static string Summary(GraphNode node, RuneDefinition rune, IEnumerable<SpellGraph> library)
        {
            if (node.RuneId == SpellGrammar.CORE_RUNE)
            {
                string trigger = node.GetText(SpellGrammar.TRIGGER_PARAM, SpellGrammar.TRIGGER_ON_ATTACK);
                return LocalizedValue("trigger." + trigger, trigger);
            }
            if (rune.Id == SpellGrammar.APPLY_RUNE)
            {
                string element = node.GetText(SpellGrammar.ELEMENT_PARAM, "healing");
                return LocalizedValue("element." + element, element);
            }
            if (node.RuneId == SpellGrammar.CALL_RUNE)
            {
                string spellId = node.GetText("spellId");
                foreach (SpellGraph graph in library)
                {
                    if (graph.Id == spellId) return graph.Name;
                }
                return string.IsNullOrEmpty(spellId) ? GameData.L("ui.selectSpell") : GameData.L("ui.missingSpell");
            }
            return null;
        }

        /// <summary>현지화 값을 찾고 번역이 없으면 지정 원문을 반환한다.</summary>
        private static string LocalizedValue(string key, string fallback)
        {
            string value = GameData.L(key);
            return value == key ? fallback : value;
        }
    }
}
