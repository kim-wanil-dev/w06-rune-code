using UnityEngine;

public sealed class WorkshopRobotView : MonoBehaviour
{
    [Header("조립 시각")]
    [SerializeField] private Transform _visualRoot;
    [SerializeField] private SpriteRenderer _legsRenderer;
    [SerializeField] private SpriteRenderer _backArmRenderer;
    [SerializeField] private SpriteRenderer _coreRenderer;
    [SerializeField] private SpriteRenderer _headRenderer;
    [SerializeField] private SpriteRenderer _frontArmRenderer;
    [SerializeField] private TextMesh _identityLabel;
    [SerializeField] private SpriteRenderer _selectionRing;

    private WorkshopSimulation _simulation;
    private string _robotId;
    private bool _facingLeft;
    private bool _hasPreviousPosition;
    private float _previousX;

    /// <summary>Connects a robot instance to its renderers and initializes the displayed parts.</summary>
    public void Configure(WorkshopSimulation simulation, WorkshopRobotState robot)
    {
        _simulation = simulation;
        _robotId = robot.InstanceId;
        if (_identityLabel != null)
        {
            _identityLabel.font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Segoe UI", "Arial" }, 32);
            _identityLabel.GetComponent<MeshRenderer>().sharedMaterial = _identityLabel.font.material;
        }

        Refresh(robot);
    }

    /// <summary>Updates the assembled sprites, activity mark, grade label, and selection highlight.</summary>
    public void Refresh(WorkshopRobotState robot)
    {
        if (_simulation == null || robot == null)
        {
            return;
        }

        PartDefinition core = _simulation.GetPartDefinition(robot.CoreDefinitionId);
        WorkshopPartInstance head = _simulation.GetEquippedPart(robot, RobotPartSlot.Head, true);
        WorkshopPartInstance legs = _simulation.GetEquippedPart(robot, RobotPartSlot.Legs, true);
        WorkshopPartInstance leftArm = _simulation.GetEquippedPart(robot, RobotPartSlot.Arms, true);
        WorkshopPartInstance rightArm = _simulation.GetEquippedPart(robot, RobotPartSlot.Arms, false);
        SetSprite(_legsRenderer, GetDefinitionSprite(legs, false));
        SetSprite(_backArmRenderer, GetDefinitionSprite(_facingLeft ? leftArm : rightArm, _facingLeft));
        SetSprite(_coreRenderer, core == null ? null : core.Icon);
        SetSprite(_headRenderer, GetDefinitionSprite(head, false));
        SetSprite(_frontArmRenderer, GetDefinitionSprite(_facingLeft ? rightArm : leftArm, !_facingLeft));

        if (_identityLabel != null)
        {
            _identityLabel.text = $"{(robot.IsFirstRobot ? "◆" : "◇")} {robot.Name}\n{_simulation.GetPhaseLabel(robot)}";
            WorkshopGradeEntry grade = _simulation.State == null ? null : GetGrade(robot.CoreGradeId);
            _identityLabel.color = grade == null ? Color.white : grade.DisplayColor;
        }
    }

    /// <summary>Moves the robot's logic root without changing its ground position or selection area.</summary>
    public void SetWorldPosition(Vector3 position)
    {
        if (_hasPreviousPosition && !Mathf.Approximately(position.x, _previousX))
        {
            SetFacing(position.x < _previousX);
        }

        transform.position = position;
        _previousX = position.x;
        _hasPreviousPosition = true;
    }

    /// <summary>Flips the rendered body when the robot travels left, leaving its logical transform unchanged.</summary>
    public void SetFacing(bool isFacingLeft)
    {
        if (_facingLeft == isFacingLeft)
        {
            return;
        }

        _facingLeft = isFacingLeft;
        Refresh(_simulation == null ? null : FindRobot());
    }

    /// <summary>Highlights a selected world robot while preserving its body renderers.</summary>
    public void SetSelected(bool isSelected)
    {
        if (_selectionRing != null)
        {
            _selectionRing.enabled = isSelected;
        }
    }

    /// <summary>Returns a left, right, or icon sprite for a robot part instance.</summary>
    private Sprite GetDefinitionSprite(WorkshopPartInstance part, bool isLeft)
    {
        PartDefinition definition = part == null ? null : _simulation.GetPartDefinition(part.DefinitionId);
        if (definition == null)
        {
            return null;
        }

        return isLeft ? definition.LeftSprite : definition.RightSprite != null ? definition.RightSprite : definition.Icon;
    }

    /// <summary>Returns the configured grade entry matching the stable grade identifier.</summary>
    private WorkshopGradeEntry GetGrade(string gradeId)
    {
        return _simulation.GradeTable.Find(gradeId);
    }

    /// <summary>Returns the current simulation record for this view robot identifier.</summary>
    private WorkshopRobotState FindRobot()
    {
        for (int i = 0; i < _simulation.Robots.Count; i++)
        {
            if (_simulation.Robots[i].InstanceId == _robotId)
            {
                return _simulation.Robots[i];
            }
        }

        return null;
    }

    /// <summary>Assigns a sprite to a renderer and enables it only when the sprite exists.</summary>
    private void SetSprite(SpriteRenderer target, Sprite sprite)
    {
        if (target == null)
        {
            return;
        }

        target.sprite = sprite;
        target.enabled = sprite != null;
    }
}
