using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BonusSpawnConfig", menuName = "Robot Workshop/Bonus Spawn Config")]
public sealed class BonusSpawnConfig : ScriptableObject
{
    [Header("생성 간격과 대기 수")]
    [SerializeField] private float _minimumIntervalSeconds = 180f;
    [SerializeField] private float _maximumIntervalSeconds = 300f;
    [SerializeField] private int _maximumWaitingBonuses = 2;

    [Header("생성 내용 및 작업 시간")]
    [SerializeField] private float _workTimeMultiplier = 0.8f;
    [SerializeField] private Vector2 _minimumWorldPosition = new Vector2(10f, 0f);
    [SerializeField] private Vector2 _maximumWorldPosition = new Vector2(14f, 0f);
    [SerializeField] private List<BonusSpawnEntry> _resourcePool = new List<BonusSpawnEntry>();

    public float MinimumIntervalSeconds => _minimumIntervalSeconds;
    public float MaximumIntervalSeconds => _maximumIntervalSeconds;
    public int MaximumWaitingBonuses => _maximumWaitingBonuses;
    public float WorkTimeMultiplier => _workTimeMultiplier;
    public Vector2 MinimumWorldPosition => _minimumWorldPosition;
    public Vector2 MaximumWorldPosition => _maximumWorldPosition;
    public IReadOnlyList<BonusSpawnEntry> ResourcePool => _resourcePool;
}
