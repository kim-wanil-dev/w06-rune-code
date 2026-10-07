using UnityEngine;

public sealed class WorkshopResourceSource : MonoBehaviour
{
    [Header("고정 공급원")]
    [SerializeField] private string _sourceId;
    [SerializeField] private ResourceDefinition _resource;
    [SerializeField] private int _initialQuantity;
    [SerializeField] private int _maximumQuantity;
    [SerializeField] private float _refillIntervalSeconds;
    [SerializeField] private int _refillQuantity = 1;
    [SerializeField] private bool _requiresExpansion;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private TextMesh _quantityLabel;

    public string SourceId => _sourceId;
    public ResourceDefinition Resource => _resource;
    public int InitialQuantity => _initialQuantity;
    public int MaximumQuantity => _maximumQuantity;
    public float RefillIntervalSeconds => _refillIntervalSeconds;
    public int RefillQuantity => _refillQuantity;
    public bool RequiresExpansion => _requiresExpansion;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    public TextMesh QuantityLabel => _quantityLabel;

    /// <summary>Shows remaining stock and disables the source image while its district is locked.</summary>
    public void SetVisualState(int quantity, bool isOpen)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = isOpen;
            _spriteRenderer.color = quantity > 0 ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
        }

        if (_quantityLabel != null)
        {
            _quantityLabel.gameObject.SetActive(isOpen);
            _quantityLabel.text = quantity.ToString();
        }
    }
}
