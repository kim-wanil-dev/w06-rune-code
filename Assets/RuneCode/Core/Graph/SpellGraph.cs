using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    [Serializable]
    public sealed class NodeParameter
    {
        [Header("파라미터 값")]
        [SerializeField] private string _key;
        [SerializeField] private float _number;
        [SerializeField] private string _text;
        public string Key => _key;
        public float Number => _number;
        public string Text => _text;

        /// <summary>키에 숫자 또는 문자열 파라미터 값을 연결한다.</summary>
        public NodeParameter(string key, float number, string text = null)
        {
            _key = key;
            _number = number;
            _text = text;
        }
    }

    [Serializable]
    public sealed class GraphNode
    {
        [Header("노드 구성")]
        [SerializeField] private string _id;
        [SerializeField] private string _runeId;
        [SerializeField] private float _x;
        [SerializeField] private float _y;
        [SerializeField] private List<NodeParameter> _params = new List<NodeParameter>();
        public string Id => _id;
        public string RuneId => _runeId;
        public float X => _x;
        public float Y => _y;
        public IReadOnlyList<NodeParameter> Params => _params;

        /// <summary>ID와 룬 종류 및 캔버스 좌표로 편집 가능한 노드를 생성한다.</summary>
        public GraphNode(string id, string runeId, float x = 0f, float y = 0f)
        {
            _id = id;
            _runeId = runeId;
            _x = x;
            _y = y;
        }

        /// <summary>캔버스 좌표만 변경하며 마법 실행 의미는 유지한다.</summary>
        public void Move(float x, float y)
        {
            _x = x;
            _y = y;
        }

        /// <summary>키의 숫자 파라미터를 읽고 값이 없으면 지정 기본값을 반환한다.</summary>
        public float GetNumber(string key, float fallback = 0f)
        {
            foreach (NodeParameter param in _params)
                if (param.Key == key) return param.Number;
            return fallback;
        }

        /// <summary>키의 문자열 파라미터를 읽고 값이 없으면 지정 기본값을 반환한다.</summary>
        public string GetText(string key, string fallback = "")
        {
            foreach (NodeParameter param in _params)
                if (param.Key == key) return param.Text;
            return fallback;
        }

        /// <summary>키의 숫자 파라미터를 교체하거나 추가한다.</summary>
        public void SetNumber(string key, float value) => SetParameter(new NodeParameter(key, value));

        /// <summary>키의 문자열 파라미터를 교체하거나 추가한다.</summary>
        public void SetText(string key, string value) => SetParameter(new NodeParameter(key, 0f, value));

        /// <summary>파라미터가 독립된 노드 복사본을 만들고 새 노드 ID를 지정한다.</summary>
        public GraphNode Clone(string newId)
        {
            GraphNode clone = new GraphNode(newId, _runeId, _x, _y);
            foreach (NodeParameter param in _params)
                clone.SetParameter(new NodeParameter(param.Key, param.Number, param.Text));
            return clone;
        }

        /// <summary>파라미터 키의 기존 값을 교체하고 새 키는 목록 끝에 추가한다.</summary>
        private void SetParameter(NodeParameter value)
        {
            for (int i = 0; i < _params.Count; i++)
            {
                if (_params[i].Key != value.Key) continue;
                _params[i] = value;
                return;
            }
            _params.Add(value);
        }
    }

    [Serializable]
    public sealed class GraphEdge
    {
        [Header("연결")]
        [SerializeField] private string _id;
        [SerializeField] private string _fromNode;
        [SerializeField] private string _fromPort;
        [SerializeField] private string _toNode;
        [SerializeField] private string _toPort;
        public string Id => _id;
        public string FromNode => _fromNode;
        public string FromPort => _fromPort;
        public string ToNode => _toNode;
        public string ToPort => _toPort;

        /// <summary>출력과 입력 노드의 포트 ID로 그래프 연결을 생성한다.</summary>
        public GraphEdge(string id, string fromNode, string fromPort, string toNode, string toPort)
        {
            _id = id;
            _fromNode = fromNode;
            _fromPort = fromPort;
            _toNode = toNode;
            _toPort = toPort;
        }
    }

    [Serializable]
    public sealed class SpellGraph
    {
        [Header("마법 구성")]
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _version = 1;
        [SerializeField] private List<GraphNode> _nodes = new List<GraphNode>();
        [SerializeField] private List<GraphEdge> _edges = new List<GraphEdge>();
        public string Id => _id;
        public string Name => _name;
        public int Version => _version;
        public IReadOnlyList<GraphNode> Nodes => _nodes;
        public IReadOnlyList<GraphEdge> Edges => _edges;

        /// <summary>마법 ID와 이름으로 편집용 그래프를 초기화한다.</summary>
        public SpellGraph(string id, string name)
        {
            _id = id;
            _name = name;
        }

        /// <summary>삭제할 수 없는 시전 Core가 포함된 새 마법을 반환한다.</summary>
        public static SpellGraph Create(string id, string name)
        {
            SpellGraph graph = new SpellGraph(id, name);
            graph.AddNode(new GraphNode("core", "core.cast", 80f, 140f));
            return graph;
        }

        /// <summary>다른 보관함 항목으로 식별할 새 마법 ID를 지정한다.</summary>
        public void SetIdentity(string id) => _id = id;

        /// <summary>마법의 표시 이름을 변경한다.</summary>
        public void Rename(string name) => _name = name;

        /// <summary>ID로 편집 노드를 찾으며 존재하지 않으면 null을 반환한다.</summary>
        public GraphNode FindNode(string id)
        {
            foreach (GraphNode node in _nodes)
                if (node.Id == id) return node;
            return null;
        }

        /// <summary>중복 ID를 차단하면서 노드를 그래프에 배치한다.</summary>
        public bool AddNode(GraphNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Id) || FindNode(node.Id) != null) return false;
            _nodes.Add(node);
            return true;
        }

        /// <summary>Core를 제외한 노드와 연결된 엣지를 제거하고 제거 성공 여부를 반환한다.</summary>
        public bool RemoveNode(string id)
        {
            GraphNode node = FindNode(id);
            if (node == null || node.RuneId == "core.cast") return false;
            _nodes.Remove(node);
            _edges.RemoveAll(edge => edge.FromNode == id || edge.ToNode == id);
            return true;
        }

        /// <summary>중복 ID 및 같은 포트 연결을 차단하며 엣지를 추가한다.</summary>
        public bool AddEdge(GraphEdge edge)
        {
            if (edge == null || string.IsNullOrEmpty(edge.Id)) return false;
            foreach (GraphEdge current in _edges)
                if (current.Id == edge.Id || (current.FromNode == edge.FromNode && current.FromPort == edge.FromPort
                    && current.ToNode == edge.ToNode && current.ToPort == edge.ToPort)) return false;
            _edges.Add(edge);
            return true;
        }

        /// <summary>ID에 해당하는 엣지를 제거하고 제거 여부를 반환한다.</summary>
        public bool RemoveEdge(string id) => _edges.RemoveAll(edge => edge.Id == id) > 0;

        /// <summary>노드와 파라미터가 독립된 복사본을 만들고 선택적으로 새 ID를 지정한다.</summary>
        public SpellGraph Clone(string id = null)
        {
            SpellGraph clone = ShareCodec.Deserialize(ShareCodec.Serialize(this));
            if (id != null) clone.SetIdentity(id);
            return clone;
        }
    }
}
