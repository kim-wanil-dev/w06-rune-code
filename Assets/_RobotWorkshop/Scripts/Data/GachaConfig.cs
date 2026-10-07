using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GachaConfig", menuName = "Robot Workshop/Gacha Config")]
public sealed class GachaConfig : ScriptableObject
{
    [Header("부위별 뽑기 비용")]
    [SerializeField] private List<GachaSlotCost> _slotCosts = new List<GachaSlotCost>();

    [Header("기본 파츠 세트")]
    [SerializeField] private int _starterKitCost = 40;
    [SerializeField] private PartDefinition _starterHead;
    [SerializeField] private PartDefinition _starterLegs;
    [SerializeField] private PartDefinition _starterMultiTool;
    [SerializeField] private PartDefinition _starterClaw;

    public IReadOnlyList<GachaSlotCost> SlotCosts => _slotCosts;
    public int StarterKitCost => _starterKitCost;
    public PartDefinition StarterHead => _starterHead;
    public PartDefinition StarterLegs => _starterLegs;
    public PartDefinition StarterMultiTool => _starterMultiTool;
    public PartDefinition StarterClaw => _starterClaw;

    /// <summary>Returns the configured price for one draw of the requested slot.</summary>
    public int GetCost(RobotPartSlot slot)
    {
        for (int i = 0; i < _slotCosts.Count; i++)
        {
            if (_slotCosts[i].Slot == slot)
            {
                return _slotCosts[i].Cost;
            }
        }

        return -1;
    }
}
