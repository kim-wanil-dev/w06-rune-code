using System;
using System.Collections.Generic;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// SpellGraph의 Unity JSON 직렬화 형식이다. 문법 엔진의 SpellGraph는 순수 C#이라 JsonUtility로 직렬화할 수 없으므로
    /// 저장 파일·공유 코드·템플릿은 이 형식을 거친다. 필드 이름과 순서가 기존 JSON 키와 같아 기존 데이터와 호환된다.
    /// </summary>
    [Serializable]
    public sealed class SpellGraphData
    {
        [Header("마법 구성")]
        [SerializeField] private string _id;
        [SerializeField] private string _name;
        [SerializeField] private int _version = 2;
        [SerializeField] private List<GraphNodeData> _nodes = new List<GraphNodeData>();
        [SerializeField] private List<GraphEdgeData> _edges = new List<GraphEdgeData>();

        /// <summary>그래프의 ID·이름·버전·노드·엣지를 직렬화 형식으로 옮긴다.</summary>
        public static SpellGraphData From(SpellGraph graph)
        {
            var data = new SpellGraphData { _id = graph.Id, _name = graph.Name, _version = graph.Version };
            foreach (GraphNode node in graph.Nodes)
            {
                data._nodes.Add(GraphNodeData.From(node));
            }
            foreach (GraphEdge edge in graph.Edges)
            {
                data._edges.Add(GraphEdgeData.From(edge));
            }
            return data;
        }

        /// <summary>직렬화 형식을 그래프로 되돌린다. 값을 정리하지 않으므로 외부 데이터는 SpellGraphValidator로 검증한다.</summary>
        public SpellGraph ToGraph()
        {
            var nodes = new List<GraphNode>();
            if (_nodes != null)
            {
                foreach (GraphNodeData node in _nodes)
                {
                    nodes.Add(node?.ToNode());
                }
            }
            var edges = new List<GraphEdge>();
            if (_edges != null)
            {
                foreach (GraphEdgeData edge in _edges)
                {
                    edges.Add(edge?.ToEdge());
                }
            }
            return new SpellGraph(_id, _name, _version, nodes, edges);
        }
    }

    /// <summary>GraphNode의 Unity JSON 직렬화 형식이다.</summary>
    [Serializable]
    public sealed class GraphNodeData
    {
        [Header("노드 구성")]
        [SerializeField] private string _id;
        [SerializeField] private string _runeId;
        [SerializeField] private float _x;
        [SerializeField] private float _y;
        [SerializeField] private List<NodeParameterData> _params = new List<NodeParameterData>();

        /// <summary>노드의 ID·룬·좌표·파라미터를 직렬화 형식으로 옮긴다.</summary>
        public static GraphNodeData From(GraphNode node)
        {
            var data = new GraphNodeData { _id = node.Id, _runeId = node.RuneId, _x = node.X, _y = node.Y };
            foreach (NodeParameter param in node.Params)
            {
                data._params.Add(NodeParameterData.From(param));
            }
            return data;
        }

        /// <summary>직렬화 형식을 노드로 되돌린다.</summary>
        public GraphNode ToNode()
        {
            var parameters = new List<NodeParameter>();
            if (_params != null)
            {
                foreach (NodeParameterData param in _params)
                {
                    parameters.Add(param?.ToParameter());
                }
            }
            return new GraphNode(_id, _runeId, _x, _y, parameters);
        }
    }

    /// <summary>NodeParameter의 Unity JSON 직렬화 형식이다.</summary>
    [Serializable]
    public sealed class NodeParameterData
    {
        [Header("파라미터 값")]
        [SerializeField] private string _key;
        [SerializeField] private float _number;
        [SerializeField] private string _text;

        /// <summary>파라미터의 키·숫자·문자열을 직렬화 형식으로 옮긴다.</summary>
        public static NodeParameterData From(NodeParameter param)
        {
            return new NodeParameterData { _key = param.Key, _number = param.Number, _text = param.Text };
        }

        /// <summary>직렬화 형식을 파라미터로 되돌린다.</summary>
        public NodeParameter ToParameter()
        {
            return new NodeParameter(_key, _number, _text);
        }
    }

    /// <summary>GraphEdge의 Unity JSON 직렬화 형식이다.</summary>
    [Serializable]
    public sealed class GraphEdgeData
    {
        [Header("연결")]
        [SerializeField] private string _id;
        [SerializeField] private string _fromNode;
        [SerializeField] private string _fromPort;
        [SerializeField] private string _toNode;
        [SerializeField] private string _toPort;
        [SerializeField] private int _order;
        [SerializeField] private bool _isLegacyEvent;

        /// <summary>엣지의 ID·양 끝 노드와 포트·순번·이전 이벤트 의미 표시를 직렬화 형식으로 옮긴다.</summary>
        public static GraphEdgeData From(GraphEdge edge)
        {
            return new GraphEdgeData
            {
                _id = edge.Id, _fromNode = edge.FromNode, _fromPort = edge.FromPort,
                _toNode = edge.ToNode, _toPort = edge.ToPort, _order = edge.Order, _isLegacyEvent = edge.IsLegacyEvent
            };
        }

        /// <summary>직렬화 형식을 엣지로 되돌린다. 키가 없는 이전 데이터는 이전 이벤트 표시가 false다.</summary>
        public GraphEdge ToEdge()
        {
            return new GraphEdge(_id, _fromNode, _fromPort, _toNode, _toPort, _order, _isLegacyEvent);
        }
    }
}
