using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneArenaGraphic : UnityEngine.UI.MaskableGraphic
    {
        private const float WORLD_WIDTH = 1280;
        private const float WORLD_HEIGHT = 704;
        private const float GENERATION_COLOR_RANGE = 4f;

        private readonly List<TextMeshProUGUI> _damageLabels = new List<TextMeshProUGUI>();
        private Func<RuneSimulation> _simulation;
        private Func<bool> _screenShake;
        private Func<bool> _hitStop;
        private TMP_FontAsset _font;
        private float _scale;
        private Vector2 _origin;
        private RuneSimulation _observedSimulation;
        private double _lastDamage;
        private double _lastHp;
        private float _shakeUntil;
        private float _hitStopUntil;

        /// <summary>렌더링할 시뮬레이션 조회 함수와 피해 숫자 글꼴을 연결한다.</summary>
        public void Initialize(Func<RuneSimulation> simulation, TMP_FontAsset font, Func<bool> screenShake = null, Func<bool> hitStop = null)
        {
            _simulation = simulation;
            _font = font;
            _screenShake = screenShake;
            _hitStop = hitStop;
            raycastTarget = true;
            SetVerticesDirty();
        }

        /// <summary>마우스 화면 위치가 경기장 내부이면 시뮬레이션 좌표를 반환한다.</summary>
        public bool TryGetPointer(Vector2 screen, out SimVector position)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out Vector2 local);
            CalculateScale();
            Vector2 point = (local - _origin) / Mathf.Max(0.001f, _scale);
            position = new SimVector(point.x, -point.y);
            return point.x >= 0 && point.x <= WORLD_WIDTH && point.y <= 0 && point.y >= -WORLD_HEIGHT;
        }

        /// <summary>시뮬레이션 위치를 경기장 RectTransform의 로컬 좌표로 변환한다.</summary>
        private Vector2 Point(SimVector position) => _origin + new Vector2((float)position.X, -(float)position.Y) * _scale;

        /// <summary>가로세로 비율을 유지하는 배율과 경기장 좌상단 위치를 계산한다.</summary>
        private void CalculateScale()
        {
            Rect bounds = rectTransform.rect;
            _scale = Mathf.Min(bounds.width / WORLD_WIDTH, bounds.height / WORLD_HEIGHT);
            _origin = new Vector2(bounds.center.x - WORLD_WIDTH * _scale / 2, bounds.center.y + WORLD_HEIGHT * _scale / 2);
            if (_shakeUntil > Time.unscaledTime && (_screenShake?.Invoke() ?? false))
                _origin += new Vector2(Mathf.Sin(Time.unscaledTime * 170), Mathf.Cos(Time.unscaledTime * 190)) * (3 * _scale);
        }

        /// <summary>투사체 세대 번호를 받아 세대 0의 청록색에서 세대가 늘수록 주황색으로 보간한 색을 반환한다.</summary>
        public static Color GenerationColor(int generation)
        {
            return Color.Lerp(new Color(0.35f, 0.9f, 1f), new Color(1f, 0.62f, 0.25f), Mathf.Clamp01(generation / GENERATION_COLOR_RANGE));
        }

        /// <summary>벽, 전투 개체와 시각적 상태를 현재 시뮬레이션 데이터로 그린다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            CalculateScale();
            RuneMesh.Rect(mesh, rectTransform.rect, new Color(0.02f, 0.04f, 0.075f));
            RuneSimulation sim = _simulation?.Invoke();
            if (sim == null) return;
            for (int row = 0; row < sim.Map.Tiles.Length; row++)
                for (int column = 0; column < sim.Map.Tiles[row].Length; column++)
                {
                    char tile = sim.Map.Tiles[row][column];
                    Vector2 corner = Point(new SimVector(column * 32, row * 32 + 32));
                    if (tile == '#')
                    {
                        RuneMesh.Rect(mesh, new Rect(corner, Vector2.one * (32 * _scale)), new Color(0.13f, 0.20f, 0.27f));
                        RuneMesh.Line(mesh, corner + new Vector2(0, 32 * _scale), corner + Vector2.one * (32 * _scale), _scale, new Color(0.23f, 0.40f, 0.47f));
                    }
                    else
                    {
                        RuneMesh.Rect(mesh, new Rect(corner, Vector2.one * _scale), new Color(0.15f, 0.22f, 0.29f));
                        if (!sim.IsTimedBattle && (tile == 'D' || tile == 'T'))
                        {
                            Color marker = sim.Stage == MissionStage.Terminal ? new Color(0.26f, 0.91f, 0.66f) : new Color(0.26f, 0.43f, 0.49f);
                            RuneMesh.Ring(mesh, corner + Vector2.one * (16 * _scale), 12 * _scale, 2 * _scale, marker, 4);
                        }
                    }
                }
            if (sim.IsTimedBattle) DrawIncomingFlow(mesh, sim);
            foreach (SpellProjectile projectile in sim.Spell.Projectiles)
            {
                Vector2 point = Point(projectile.Position);
                Vector2 direction = new Vector2((float)projectile.Direction.X, -(float)projectile.Direction.Y);
                Color tint = GenerationColor(projectile.Generation);
                float radius = Mathf.Max(2.5f, GameData.Balance.Spell.ProjectileRadius * _scale);
                Color trail = tint; trail.a = 0.25f;
                RuneMesh.Line(mesh, point - direction * 24 * _scale, point, radius, trail);
                RuneMesh.Polygon(mesh, point, radius, tint, 16, Mathf.Atan2(direction.y, direction.x));
                if (projectile.CarriedMana > 0) RuneMesh.Ring(mesh, point, radius + 4 * _scale, 1.5f * _scale, new Color(1f, 0.95f, 0.6f), 12);
            }
            foreach (SimulationProjectile projectile in sim.EnemyProjectiles)
            {
                Vector2 point = Point(projectile.Position);
                float radius = (float)projectile.Radius * _scale;
                Color tint = new Color(1, 0.33f, 0.42f);
                if (projectile.IsHazard)
                {
                    Color fill = tint; fill.a = projectile.IsWarning ? 0.10f : 0.30f;
                    RuneMesh.Polygon(mesh, point, radius, fill);
                    RuneMesh.Ring(mesh, point, radius, 2 * _scale, tint);
                    RuneMesh.Line(mesh, point - Vector2.one * radius * 0.3f, point + Vector2.one * radius * 0.3f, 2 * _scale, tint);
                }
                else
                {
                    RuneMesh.Polygon(mesh, point, radius, tint, 4);
                    RuneMesh.Line(mesh, point - new Vector2((float)projectile.Direction.X, -(float)projectile.Direction.Y) * 14 * _scale,
                        point, radius, new Color(1, 0.25f, 0.3f, 0.25f));
                }
            }
            foreach (FragmentOrb orb in sim.Orbs)
            {
                Vector2 point = Point(orb.Position);
                RuneMesh.Polygon(mesh, point, 6 * _scale, new Color(0.43f, 1f, 0.82f), 4);
                RuneMesh.Ring(mesh, point, 9 * _scale, _scale, new Color(0.3f, 0.9f, 0.7f, 0.3f), 4);
            }
            foreach (SimulationEnemy enemy in sim.Enemies) DrawEnemy(mesh, enemy, sim);
            SimulationPlayer player = sim.Player;
            Vector2 playerPoint = Point(player.Position);
            Vector2 aim = new Vector2((float)player.AimDirection.X, -(float)player.AimDirection.Y);
            Color playerColor = player.DashRemaining > 0 ? new Color(0.75f, 1, 1) : new Color(0.35f, 0.91f, 0.99f);
            if (player.DashRemaining > 0) RuneMesh.Line(mesh, playerPoint - aim * 44 * _scale, playerPoint, 14 * _scale, new Color(0.3f, 0.9f, 1, 0.25f));
            float playerRadius = sim.Stage == MissionStage.Bench ? Mathf.Max(4.5f, 12 * _scale) : 12 * _scale;
            RuneMesh.Polygon(mesh, playerPoint, playerRadius, playerColor, 3, Mathf.Atan2(aim.y, aim.x));
            RuneMesh.Ring(mesh, playerPoint, 17 * _scale, _scale, new Color(0.2f, 0.75f, 0.93f, 0.5f));
            RuneMesh.Line(mesh, playerPoint, playerPoint + aim * Mathf.Max(sim.Stage == MissionStage.Bench ? 8 : 0, 27 * _scale),
                Mathf.Max(sim.Stage == MissionStage.Bench ? 1 : 0, 3 * _scale), new Color(0.92f, 0.83f, 0.49f));
            if (player.Shield > 0) RuneMesh.Ring(mesh, playerPoint, 23 * _scale, 2 * _scale, new Color(0.65f, 0.53f, 1));
        }

        /// <summary>시간제 전투에서 오른쪽의 적이 왼쪽 플레이어에게 오는 흐름을 배경 도형으로 표시한다.</summary>
        private void DrawIncomingFlow(UnityEngine.UI.VertexHelper mesh, RuneSimulation sim)
        {
            Color lane = new Color(0.18f, 0.47f, 0.59f, 0.15f);
            for (int row = 1; row <= 3; row++)
            {
                double y = WORLD_HEIGHT * row / 4;
                RuneMesh.Line(mesh, Point(new SimVector(sim.Player.Position.X + 70, y)), Point(new SimVector(WORLD_WIDTH - 64, y)), _scale, lane);
                for (double x = sim.Player.Position.X + 160; x < WORLD_WIDTH - 64; x += 150)
                    RuneMesh.Polygon(mesh, Point(new SimVector(x, y)), 4 * _scale, lane, 3, Mathf.PI);
            }
            RuneMesh.Ring(mesh, Point(sim.Player.Position), 33 * _scale, 1.5f * _scale, new Color(0.26f, 0.75f, 0.86f, 0.35f), 12);
        }

        /// <summary>적 종류에 따른 도형과 체력, 공격 예고, 상태 표시를 그린다.</summary>
        private void DrawEnemy(UnityEngine.UI.VertexHelper mesh, SimulationEnemy enemy, RuneSimulation sim)
        {
            Vector2 point = Point(enemy.Position);
            float radius = sim.Stage == MissionStage.Bench ? Mathf.Max(5, (float)enemy.Radius * _scale) : (float)enemy.Radius * _scale;
            string kind = enemy.Kind;
            int sides = kind == "enemy.scout" ? 3 : kind == "enemy.sentry" ? 4 : kind == "enemy.hunter" ? 4 : kind == "enemy.aegis" ? 6 : kind == "enemy.relay" ? 8 : 6;
            Color tint = kind == "enemy.relay" ? new Color(0.62f, 0.39f, 0.97f) : kind == "enemy.aegis" ? new Color(0.96f, 0.69f, 0.29f) :
                kind == "boss.governor" ? new Color(0.89f, 0.35f, 0.92f) : new Color(0.94f, 0.34f, 0.40f);
            if (enemy.IsDummy) tint = new Color(0.52f, 0.69f, 0.74f);
            if (enemy.IsFlashing) tint = Color.white;
            float facing = Mathf.Atan2(-(float)enemy.Facing.Y, (float)enemy.Facing.X);
            RuneMesh.Polygon(mesh, point, radius, tint, sides, facing);
            RuneMesh.Polygon(mesh, point, radius * 0.48f, new Color(0.04f, 0.09f, 0.13f), sides, facing);
            if (enemy.IsWarning) RuneMesh.Ring(mesh, point, radius + (6 + Mathf.Sin(Time.unscaledTime * 16) * 3) * _scale, 2 * _scale, new Color(1, 0.78f, 0.23f));
            if (kind == "enemy.relay") RuneMesh.Ring(mesh, point, GameData.Balance.Combat.RelayRadius * _scale, _scale, new Color(0.65f, 0.37f, 0.95f, 0.22f));
            if (kind == "enemy.aegis" && !enemy.IsEmp)
            {
                for (int i = -5; i < 5; i++)
                {
                    float from = facing + i * Mathf.PI / 15;
                    float to = from + Mathf.PI / 15;
                    RuneMesh.Line(mesh, point + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * (radius + 5 * _scale),
                        point + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * (radius + 5 * _scale), 3 * _scale, new Color(0.95f, 0.77f, 0.40f));
                }
            }
            if (enemy.IsPatching) RuneMesh.Ring(mesh, point, radius + 12 * _scale, 3 * _scale, new Color(0.83f, 0.57f, 1), 12);
            if (enemy.IsBurning) RuneMesh.Polygon(mesh, point + new Vector2(-radius, radius + 8 * _scale), 4 * _scale, RuneMesh.ElementColor("fire"), 3);
            if (enemy.ChillStacks > 0 || enemy.IsFrozen) RuneMesh.Polygon(mesh, point + new Vector2(0, radius + 8 * _scale), 4 * _scale, RuneMesh.ElementColor("ice"), 6);
            if (enemy.IsEmp) RuneMesh.Polygon(mesh, point + new Vector2(radius, radius + 8 * _scale), 4 * _scale, RuneMesh.ElementColor("arc"), 4);
            float hpWidth = Math.Max(28, radius * 2);
            RuneMesh.Rect(mesh, new Rect(point.x - hpWidth / 2, point.y - radius - 8 * _scale, hpWidth, 3 * _scale), new Color(0.24f, 0.16f, 0.20f));
            RuneMesh.Rect(mesh, new Rect(point.x - hpWidth / 2, point.y - radius - 8 * _scale, hpWidth * (float)(enemy.Hp / enemy.MaxHp), 3 * _scale), tint);
        }

        void Update()
        {
            RuneSimulation sim = _simulation?.Invoke();
            if (sim == null) return;
            if (_observedSimulation != sim)
            { _observedSimulation = sim; _lastDamage = sim.TotalDamage; _lastHp = sim.Player.Hp; }
            if (sim.TotalDamage > _lastDamage || sim.Player.Hp < _lastHp)
            {
                _shakeUntil = Time.unscaledTime + 0.08f;
                _hitStopUntil = Time.unscaledTime + 0.025f;
            }
            _lastDamage = sim.TotalDamage; _lastHp = sim.Player.Hp;
            if (_hitStopUntil > Time.unscaledTime && (_hitStop?.Invoke() ?? false)) return;
            SetVerticesDirty();
            CalculateScale();
            int visible = 0;
            foreach (DamageNumber number in sim.DamageNumbers)
            {
                int elapsed = sim.Tick - number.Tick;
                if (elapsed > 40 || visible >= 64) continue;
                if (visible >= _damageLabels.Count)
                {
                    var target = new GameObject("DamageNumber", typeof(RectTransform), typeof(TextMeshProUGUI));
                    target.transform.SetParent(transform, false);
                    TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
                    text.font = _font;
                    text.fontSize = Math.Max(8, 18 * _scale);
                    text.raycastTarget = false;
                    text.alignment = TextAlignmentOptions.Center;
                    text.rectTransform.sizeDelta = new Vector2(70, 28);
                    text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0, 1);
                    _damageLabels.Add(text);
                }
                TextMeshProUGUI label = _damageLabels[visible++];
                label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = Point(number.Position) + new Vector2(0, (12 + elapsed * 0.6f) * _scale);
                label.fontSize = Math.Max(8, 18 * _scale);
                Color tint = RuneMesh.ElementColor(number.Element); tint.a = 1 - elapsed / 42f;
                label.color = tint;
                label.text = number.Amount.ToString("0.#");
            }
            for (int i = visible; i < _damageLabels.Count; i++) _damageLabels[i].gameObject.SetActive(false);
        }
    }
}
