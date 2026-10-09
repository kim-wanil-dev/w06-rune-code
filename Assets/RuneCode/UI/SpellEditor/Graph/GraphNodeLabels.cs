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
        private const float LABEL_PADDING_X = 14f;
        private const float LABEL_PADDING_TOP = 10f;
        private const float LABEL_PADDING_BOTTOM = 4f;
        private const float LABEL_WIDTH = GraphNodeLayout.NODE_WIDTH - LABEL_PADDING_X * 2;

        private readonly RectTransform _parent;
        private readonly TMP_FontAsset _font;
        private readonly GraphViewport _viewport;
        private readonly GraphNodeLayout _layout;
        private readonly Dictionary<string, TextMeshProUGUI> _labels = new Dictionary<string, TextMeshProUGUI>();
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
                string text = BuildText(node, label, library);
                if (label.text != text) label.text = text;
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
                textRect.sizeDelta = new Vector2(LABEL_WIDTH, _layout.NodeHeight(node, rune) - LABEL_PADDING_TOP - LABEL_PADDING_BOTTOM);
                textRect.localScale = scale;
            }
        }

        /// <summary>노드 ID의 라벨을 반환하며 없으면 고정 글꼴 크기와 좌상단 기준점으로 새로 만든다.</summary>
        private TextMeshProUGUI GetOrCreate(string nodeId)
        {
            if (_labels.TryGetValue(nodeId, out TextMeshProUGUI label)) return label;
            var target = new GameObject("NodeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            target.transform.SetParent(_parent, false);
            label = target.GetComponent<TextMeshProUGUI>();
            label.font = _font;
            label.fontSize = LABEL_FONT_SIZE;
            label.color = Color.white;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            RectTransform textRect = label.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0, 1);
            textRect.pivot = new Vector2(0, 1);
            _labels.Add(nodeId, label);
            return label;
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
            }
        }

        /// <summary>
        /// 룬 이름·RAM, 핵심 설정 요약, 입력·출력 포트 이름 행으로 라벨 rich text를 만든다.
        /// 출력 포트 이름은 라벨 글꼴로 너비를 재어 노드 오른쪽 끝에 맞추고, 지원 중단 포트는 회색 취소선으로 표시한다.
        /// </summary>
        private string BuildText(GraphNode node, TextMeshProUGUI label, IEnumerable<SpellGraph> library)
        {
            RuneDefinition rune = GameData.Runes.Get(node.RuneId);
            _builder.Clear();
            _builder.Append("<b>").Append(rune.Name).Append("</b>  <color=#8EACC6>").Append(GameData.Runes.NodeRam(node)).Append(" RAM</color>\n");
            string summary = Summary(node, rune, library);
            if (!string.IsNullOrEmpty(summary)) _builder.Append("<size=10><color=#A8C9D8>").Append(summary).Append("</color></size>\n");

            int rows = _layout.PortRowCount(node, rune);
            for (int row = 0; row < rows; row++)
            {
                PortDefinition input = _layout.PortAt(node, rune, SpellGrammar.DIRECTION_IN, row);
                PortDefinition output = _layout.PortAt(node, rune, SpellGrammar.DIRECTION_OUT, row);
                _builder.Append("<size=10>").Append(input?.Id);
                if (output != null)
                {
                    string outputText = output.IsDeprecated ? "<s><color=#737880>" + output.Id + "</color></s>" : output.Id;
                    float outputWidth = label.GetPreferredValues("<size=10>" + outputText).x;
                    float outputStart = Mathf.Max(0, LABEL_WIDTH - outputWidth);
                    _builder.Append("<pos=").Append(outputStart.ToString("0.#", CultureInfo.InvariantCulture)).Append('>').Append(outputText);
                }
                _builder.Append("</size>\n");
            }
            return _builder.ToString();
        }

        /// <summary>Trigger·Shape·Apply·프리셋 호출의 핵심 값(시작 조건, 속성, 호출 대상)을 반환하며 해당 없으면 null을 반환한다.</summary>
        private static string Summary(GraphNode node, RuneDefinition rune, IEnumerable<SpellGraph> library)
        {
            if (node.RuneId == SpellGrammar.CORE_RUNE)
            {
                string trigger = node.GetText(SpellGrammar.TRIGGER_PARAM, SpellGrammar.TRIGGER_ON_ATTACK);
                return LocalizedValue("trigger." + trigger, trigger);
            }
            if (rune.Category == SpellGrammar.CATEGORY_SHAPE || rune.Id == SpellGrammar.APPLY_RUNE)
            {
                string fallback = rune.Id == SpellGrammar.APPLY_RUNE ? "healing" : SpellGrammar.ELEMENT_NEUTRAL;
                string element = node.GetText(SpellGrammar.ELEMENT_PARAM, fallback);
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
