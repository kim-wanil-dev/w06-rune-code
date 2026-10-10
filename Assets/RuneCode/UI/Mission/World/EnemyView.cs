using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>
    /// 미션 적 하나의 몸체, 체력 막대, 엘리트 링, 공격 예고, 릴레이 오라, 이지스 방패, 보스 패치, 상태 이상·내성 표시를 갱신한다.
    /// 몸체와 방패는 회전 자식에 두어 적이 바라보는 방향으로 돌리고, 나머지 표시는 회전하지 않는다.
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float CORE_SCALE = 0.48f;
        private const float ELITE_GAP = 5f;
        private const float WARNING_GAP = 6f;
        private const float WARNING_PULSE = 3f;
        private const float WARNING_PULSE_SPEED = 16f;
        private const float SHIELD_GAP = 5f;
        private const float PATCH_GAP = 12f;
        private const float ICON_GAP = 8f;
        private const float ICON_RADIUS = 4f;
        private const float HP_BAR_MIN_WIDTH = 28f;
        private const float HP_BAR_HEIGHT = 3f;

        private static readonly Color CoreColor = new Color(0.04f, 0.09f, 0.13f);
        private static readonly Color DummyColor = new Color(0.52f, 0.69f, 0.74f);
        private static readonly Color EliteHpColor = new Color(1f, 0.76f, 0.24f);
        private static readonly Color EliteSpeedColor = new Color(0.42f, 0.9f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.78f, 0.23f);
        private static readonly Color AuraColor = new Color(0.65f, 0.37f, 0.95f, 0.22f);
        private static readonly Color ShieldColor = new Color(0.95f, 0.77f, 0.40f);
        private static readonly Color PatchColor = new Color(0.83f, 0.57f, 1f);
        private static readonly Color ResistColor = new Color(0.77f, 0.69f, 1f);
        private static readonly Color HpBackColor = new Color(0.24f, 0.16f, 0.20f);

        [Header("종류별 모양")]
        [SerializeField] private EnemyAppearance[] _appearances;
        [SerializeField] private Sprite _defaultShape;
        [SerializeField] private Color _defaultTint = new Color(0.94f, 0.34f, 0.40f);

        [Header("표시")]
        [SerializeField] private Transform _rotator;
        [SerializeField] private SpriteRenderer _body;
        [SerializeField] private SpriteRenderer _core;
        [SerializeField] private SpriteRenderer _shieldArc;
        [SerializeField] private SpriteRenderer _warningRing;
        [SerializeField] private SpriteRenderer _auraRing;
        [SerializeField] private SpriteRenderer _patchRing;
        [SerializeField] private SpriteRenderer _eliteRing;
        [SerializeField] private SpriteRenderer _burnIcon;
        [SerializeField] private SpriteRenderer _chillIcon;
        [SerializeField] private SpriteRenderer _empIcon;
        [SerializeField] private SpriteRenderer _resistRing;
        [SerializeField] private SpriteRenderer _hpBack;
        [SerializeField] private SpriteRenderer _hpFill;

        /// <summary>적 상태와 시뮬레이션의 상태 이상·적응 정보로 맵 중앙(origin) 기준 월드 표시를 갱신한다.</summary>
        public void Apply(SimulationEnemy enemy, RuneSimulation sim, SimVector origin)
        {
            float radius = (float)enemy.Radius;
            transform.localPosition = MissionWorldSpace.ToWorld(enemy.Position, origin);
            _rotator.localRotation = Quaternion.Euler(0, 0, MissionWorldSpace.ToWorldAngle(enemy.Facing));
            EnemyAppearance appearance = FindAppearance(enemy.Kind);
            Color tint = appearance != null ? appearance.Tint : _defaultTint;
            if (enemy.IsDummy) tint = DummyColor;
            if (enemy.IsFlashing) tint = Color.white;
            _body.sprite = _core.sprite = appearance != null ? appearance.Shape : _defaultShape;
            MissionWorldSpace.Place(_body, Vector2.zero, radius * 2, tint);
            MissionWorldSpace.Place(_core, Vector2.zero, radius * 2 * CORE_SCALE, CoreColor);
            MissionWorldSpace.SetVisible(_eliteRing, enemy.IsElite);
            // 강화형은 금색, 신속형은 청록색 링으로 구분한다. 두 배율을 모두 가지면 속도 색을 우선한다.
            if (enemy.IsElite) MissionWorldSpace.Place(_eliteRing, Vector2.zero, (radius + ELITE_GAP) * 2,
                enemy.SpeedMultiplier > 1 ? EliteSpeedColor : EliteHpColor);

            MissionWorldSpace.SetVisible(_warningRing, enemy.IsWarning);
            if (enemy.IsWarning)
                MissionWorldSpace.Place(_warningRing, Vector2.zero, (radius + WARNING_GAP + Mathf.Sin(Time.unscaledTime * WARNING_PULSE_SPEED) * WARNING_PULSE) * 2, WarningColor);
            bool isRelay = enemy.Definition.HasTrait(EnemyDefinition.TRAIT_RELAY_AURA);
            MissionWorldSpace.SetVisible(_auraRing, isRelay);
            if (isRelay) MissionWorldSpace.Place(_auraRing, Vector2.zero, (float)GameData.Balance.Combat.RelayRadius * 2, AuraColor);
            bool hasShield = enemy.Definition.HasTrait(EnemyDefinition.TRAIT_AEGIS_SHIELD) && !sim.HasEnemyStatus(enemy, EnemyStatusType.Emp);
            MissionWorldSpace.SetVisible(_shieldArc, hasShield);
            if (hasShield) MissionWorldSpace.Place(_shieldArc, Vector2.zero, (radius + SHIELD_GAP) * 2, ShieldColor);
            MissionWorldSpace.SetVisible(_patchRing, enemy.IsPatching);
            if (enemy.IsPatching) MissionWorldSpace.Place(_patchRing, Vector2.zero, (radius + PATCH_GAP) * 2, PatchColor);

            ApplyIcon(_burnIcon, sim.HasEnemyStatus(enemy, EnemyStatusType.Burn), new Vector2(-radius, radius + ICON_GAP), "fire");
            ApplyIcon(_chillIcon, sim.GetChillStacks(enemy) > 0 || sim.HasEnemyStatus(enemy, EnemyStatusType.Freeze), new Vector2(0, radius + ICON_GAP), "ice");
            ApplyIcon(_empIcon, sim.HasEnemyStatus(enemy, EnemyStatusType.Emp), new Vector2(radius, radius + ICON_GAP), "arc");
            double threshold = GameData.Balance.Adaptation.ResistanceThreshold;
            bool isResistant = sim.Adaptation.Enabled && (sim.Adaptation.GetValue(enemy.LastDamageElement) >= threshold || sim.Adaptation.GetValue(enemy.LastDamageForm) >= threshold);
            MissionWorldSpace.SetVisible(_resistRing, isResistant);
            if (isResistant) MissionWorldSpace.Place(_resistRing, new Vector2(radius + ICON_GAP, radius), ICON_RADIUS * 2, ResistColor);

            float width = Mathf.Max(HP_BAR_MIN_WIDTH, radius * 2);
            float barY = -radius - ICON_GAP + HP_BAR_HEIGHT * 0.5f;
            float ratio = Mathf.Clamp01((float)(enemy.Hp / enemy.MaxHp));
            MissionWorldSpace.PlaceLine(_hpBack, new Vector2(-width * 0.5f, barY), new Vector2(width * 0.5f, barY), HP_BAR_HEIGHT, HpBackColor);
            MissionWorldSpace.SetVisible(_hpFill, ratio > 0);
            if (ratio > 0) MissionWorldSpace.PlaceLine(_hpFill, new Vector2(-width * 0.5f, barY), new Vector2(-width * 0.5f + width * ratio, barY), HP_BAR_HEIGHT, tint);
        }

        /// <summary>상태 아이콘을 표시 여부에 따라 켜고 적 기준 위치(px)에 속성 색으로 둔다.</summary>
        private void ApplyIcon(SpriteRenderer icon, bool isVisible, Vector2 localPixels, string element)
        {
            MissionWorldSpace.SetVisible(icon, isVisible);
            if (isVisible) MissionWorldSpace.Place(icon, localPixels, ICON_RADIUS * 2, RuneMesh.ElementColor(element));
        }

        /// <summary>적 ID에 해당하는 모양 설정을 반환하며 없으면 null을 반환한다.</summary>
        private EnemyAppearance FindAppearance(string enemyId)
        {
            for (int i = 0; i < _appearances.Length; i++) if (_appearances[i].EnemyId == enemyId) return _appearances[i];
            return null;
        }

        [Serializable]
        public sealed class EnemyAppearance
        {
            [SerializeField] private string _enemyId;
            [SerializeField] private Sprite _shape;
            [SerializeField] private Color _tint = Color.white;
            public string EnemyId => _enemyId;
            public Sprite Shape => _shape;
            public Color Tint => _tint;
        }
    }
}
