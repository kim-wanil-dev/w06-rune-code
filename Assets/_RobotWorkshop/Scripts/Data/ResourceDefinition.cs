using UnityEngine;

[CreateAssetMenu(fileName = "ResourceDefinition", menuName = "Robot Workshop/Resource Definition")]
public sealed class ResourceDefinition : ScriptableObject
{
    [Header("자원 정의")]
    [SerializeField] private string _id;
    [SerializeField] private ResourceKind _kind;
    [SerializeField] private string _displayName;

    [Header("무게 및 보상")]
    [SerializeField] private float _unitWeight = 1f;
    [SerializeField] private int _scrapValue = 1;
    [SerializeField] private int _experiencePerUnit = 1;
    [SerializeField] private float _baseWorkSeconds = 3f;
    [SerializeField] private int _quantityPerGather = 1;

    [Header("표시 이미지")]
    [SerializeField] private Sprite _worldSprite;
    [SerializeField] private Sprite _bonusSprite;
    [SerializeField] private Sprite _icon;

    public string Id => _id;
    public ResourceKind Kind => _kind;
    public string DisplayName => _displayName;
    public float UnitWeight => _unitWeight;
    public int ScrapValue => _scrapValue;
    public int ExperiencePerUnit => _experiencePerUnit;
    public float BaseWorkSeconds => _baseWorkSeconds;
    public int QuantityPerGather => _quantityPerGather;
    public Sprite WorldSprite => _worldSprite;
    public Sprite BonusSprite => _bonusSprite;
    public Sprite Icon => _icon;
}
