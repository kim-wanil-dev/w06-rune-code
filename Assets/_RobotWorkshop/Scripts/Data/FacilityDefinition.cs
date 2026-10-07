using UnityEngine;

[CreateAssetMenu(fileName = "FacilityDefinition", menuName = "Robot Workshop/Facility Definition")]
public sealed class FacilityDefinition : ScriptableObject
{
    [Header("시설 정의")]
    [SerializeField] private string _id;
    [SerializeField] private string _displayName;
    [SerializeField] private int _cost;
    [SerializeField] private float _storageCapacityIncrease;
    [SerializeField] private int _activeRobotIncrease;
    [SerializeField] private bool _unlocksDistrict;

    [Header("미복구·복구·업그레이드 이미지")]
    [SerializeField] private Sprite _damagedSprite;
    [SerializeField] private Sprite _restoredSprite;
    [SerializeField] private Sprite _upgradedSprite;

    public string Id => _id;
    public string DisplayName => _displayName;
    public int Cost => _cost;
    public float StorageCapacityIncrease => _storageCapacityIncrease;
    public int ActiveRobotIncrease => _activeRobotIncrease;
    public bool UnlocksDistrict => _unlocksDistrict;
    public Sprite DamagedSprite => _damagedSprite;
    public Sprite RestoredSprite => _restoredSprite;
    public Sprite UpgradedSprite => _upgradedSprite;
}
