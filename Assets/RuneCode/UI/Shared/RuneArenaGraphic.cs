using System;
using System.Collections.Generic;

using UnityEngine;

using TMPro;

namespace RuneCode
{
    /// <summary>
    /// 전투 시뮬레이션(미션·시험 도크)을 그리는 경기장 View다. 맵 타일, 마법 개체, 적 탄환, 조각, 적과 플레이어를 메시로 그리고
    /// 피해 숫자를 재사용 라벨로 표시한다. 화면 흔들림·히트스톱은 설정 조회 함수가 켜져 있을 때만 적용한다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuneArenaGraphic : UnityEngine.UI.MaskableGraphic
    {
        private const float WORLD_WIDTH = 1280;
        private const float WORLD_HEIGHT = 704;
        private const float TILE_SIZE = 32;
        private const float SHAKE_SECONDS = 0.08f;
        private const float HIT_STOP_SECONDS = 0.025f;
        private const int DAMAGE_NUMBER_TICKS = 40;
        private const int MAX_DAMAGE_NUMBERS = 64;

        private static readonly Color BACKGROUND_COLOR = new Color(0.02f, 0.04f, 0.075f);
        private static readonly Color WALL_COLOR = new Color(0.13f, 0.20f, 0.27f);
        private static readonly Color WALL_EDGE_COLOR = new Color(0.23f, 0.40f, 0.47f);
        private static readonly Color FLOOR_DOT_COLOR = new Color(0.15f, 0.22f, 0.29f);
        private static readonly Color TERMINAL_ACTIVE_COLOR = new Color(0.26f, 0.91f, 0.66f);
        private static readonly Color TERMINAL_IDLE_COLOR = new Color(0.26f, 0.43f, 0.49f);
        private static readonly Color HOSTILE_COLOR = new Color(1, 0.33f, 0.42f);
        private static readonly Color PERSIST_WARNING_COLOR = new Color(1f, 0.25f, 0.25f);
        private static readonly Color HOSTILE_TRAIL_COLOR = new Color(1, 0.25f, 0.3f, 0.25f);
        private static readonly Color ORB_COLOR = new Color(0.43f, 1f, 0.82f);
        private static readonly Color ORB_RING_COLOR = new Color(0.3f, 0.9f, 0.7f, 0.3f);
        private static readonly Color PLAYER_COLOR = new Color(0.35f, 0.91f, 0.99f);
        private static readonly Color PLAYER_DASH_COLOR = new Color(0.75f, 1, 1);
        private static readonly Color PLAYER_DASH_TRAIL_COLOR = new Color(0.3f, 0.9f, 1, 0.25f);
        private static readonly Color PLAYER_RING_COLOR = new Color(0.2f, 0.75f, 0.93f, 0.5f);
        private static readonly Color PLAYER_AIM_COLOR = new Color(0.92f, 0.83f, 0.49f);
        private static readonly Color SHIELD_COLOR = new Color(0.65f, 0.53f, 1);
        private static readonly Color ENEMY_COLOR = new Color(0.94f, 0.34f, 0.40f);
        private static readonly Color RELAY_COLOR = new Color(0.62f, 0.39f, 0.97f);
        private static readonly Color RELAY_AURA_COLOR = new Color(0.65f, 0.37f, 0.95f, 0.22f);
        private static readonly Color AEGIS_COLOR = new Color(0.96f, 0.69f, 0.29f);
        private static readonly Color AEGIS_SHIELD_COLOR = new Color(0.95f, 0.77f, 0.40f);
        private static readonly Color BOSS_COLOR = new Color(0.89f, 0.35f, 0.92f);
        private static readonly Color DUMMY_COLOR = new Color(0.52f, 0.69f, 0.74f);
        private static readonly Color ENEMY_CORE_COLOR = new Color(0.04f, 0.09f, 0.13f);
        private static readonly Color WARNING_COLOR = new Color(1, 0.78f, 0.23f);
        private static readonly Color ELITE_HP_COLOR = new Color(1f, 0.76f, 0.24f);
        private static readonly Color ELITE_SPEED_COLOR = new Color(0.42f, 0.9f, 1f);
        private static readonly Color PATCH_COLOR = new Color(0.83f, 0.57f, 1);
        private static readonly Color RESISTANT_COLOR = new Color(0.77f, 0.69f, 1);
        private static readonly Color HP_TRACK_COLOR = new Color(0.24f, 0.16f, 0.20f);

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

        /// <summary>렌더링할 시뮬레이션 조회 함수, 피해 숫자 글꼴과 화면 흔들림·히트스톱 설정 조회 함수를 연결한다.</summary>
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

        void Update()
        {
            RuneSimulation sim = _simulation?.Invoke();
            if (sim == null) return;
            DetectImpact(sim);
            if (_hitStopUntil > Time.unscaledTime && (_hitStop?.Invoke() ?? false)) return;
            SetVerticesDirty();
            CalculateScale();
            UpdateDamageNumbers(sim);
        }

        /// <summary>벽, 전투 개체와 시각적 상태를 현재 시뮬레이션 데이터로 그린다.</summary>
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            CalculateScale();
            RuneMesh.Rect(mesh, rectTransform.rect, BACKGROUND_COLOR);
            RuneSimulation sim = _simulation?.Invoke();
            if (sim == null) return;
            DrawMap(mesh, sim);
            foreach (SimulationSpellEntity spell in sim.SpellEntities) DrawSpell(mesh, spell, sim.IsAreaBoxUpright);
            foreach (SimulationProjectile projectile in sim.EnemyProjectiles) DrawHostileProjectile(mesh, projectile);
            foreach (FragmentOrb orb in sim.Orbs) DrawOrb(mesh, orb);
            foreach (SimulationEnemy enemy in sim.Enemies) DrawEnemy(mesh, enemy, sim);
            DrawPlayer(mesh, sim);
        }

        /// <summary>시뮬레이션 위치를 경기장 RectTransform의 로컬 좌표로 변환한다.</summary>
        private Vector2 Point(SimVector position) => _origin + new Vector2((float)position.X, -(float)position.Y) * _scale;

        /// <summary>시뮬레이션 방향을 화면 방향(y 반전)으로 변환한다.</summary>
        private static Vector2 ScreenDirection(SimVector direction) => new Vector2((float)direction.X, -(float)direction.Y);

        /// <summary>가로세로 비율을 유지하는 배율과 경기장 좌상단 위치를 계산하고, 화면 흔들림 중이면 위치를 흔든다.</summary>
        private void CalculateScale()
        {
            Rect bounds = rectTransform.rect;
            _scale = Mathf.Min(bounds.width / WORLD_WIDTH, bounds.height / WORLD_HEIGHT);
            _origin = new Vector2(bounds.center.x - WORLD_WIDTH * _scale / 2, bounds.center.y + WORLD_HEIGHT * _scale / 2);
            if (_shakeUntil > Time.unscaledTime && (_screenShake?.Invoke() ?? false))
                _origin += new Vector2(Mathf.Sin(Time.unscaledTime * 170), Mathf.Cos(Time.unscaledTime * 190)) * (3 * _scale);
        }

        /// <summary>총 피해가 늘거나 플레이어 체력이 줄었으면 화면 흔들림·히트스톱 시간을 시작한다. 시뮬레이션이 바뀌면 기준값을 다시 잡는다.</summary>
        private void DetectImpact(RuneSimulation sim)
        {
            if (_observedSimulation != sim)
            {
                _observedSimulation = sim;
                _lastDamage = sim.TotalDamage;
                _lastHp = sim.Player.Hp;
            }
            if (sim.TotalDamage > _lastDamage || sim.Player.Hp < _lastHp)
            {
                _shakeUntil = Time.unscaledTime + SHAKE_SECONDS;
                _hitStopUntil = Time.unscaledTime + HIT_STOP_SECONDS;
            }
            _lastDamage = sim.TotalDamage;
            _lastHp = sim.Player.Hp;
        }

        /// <summary>벽 타일과 바닥 점, 시험 도크의 문·터미널 표시를 그린다.</summary>
        private void DrawMap(UnityEngine.UI.VertexHelper mesh, RuneSimulation sim)
        {
            float tile = TILE_SIZE * _scale;
            for (int row = 0; row < sim.Map.Tiles.Length; row++)
            {
                for (int column = 0; column < sim.Map.Tiles[row].Length; column++)
                {
                    char cell = sim.Map.Tiles[row][column];
                    Vector2 corner = Point(new SimVector(column * TILE_SIZE, row * TILE_SIZE + TILE_SIZE));
                    if (cell == '#')
                    {
                        RuneMesh.Rect(mesh, new Rect(corner, Vector2.one * tile), WALL_COLOR);
                        RuneMesh.Line(mesh, corner + new Vector2(0, tile), corner + Vector2.one * tile, _scale, WALL_EDGE_COLOR);
                        continue;
                    }
                    RuneMesh.Rect(mesh, new Rect(corner, Vector2.one * _scale), FLOOR_DOT_COLOR);
                    if (cell != 'D' && cell != 'T') continue;
                    Color marker = sim.Stage == MissionStage.Terminal ? TERMINAL_ACTIVE_COLOR : TERMINAL_IDLE_COLOR;
                    RuneMesh.Ring(mesh, corner + Vector2.one * (tile / 2), 12 * _scale, 2 * _scale, marker, 4);
                }
            }
        }

        /// <summary>마법 개체를 범위형(폭발·잔류)과 이동형(발사·공전)으로 나눠 그린다.</summary>
        private void DrawSpell(UnityEngine.UI.VertexHelper mesh, SimulationSpellEntity spell, bool isAreaBoxUpright)
        {
            Vector2 point = Point(spell.Position);
            Color tint = RuneMesh.ElementColor(spell.Element);
            float radius = (float)spell.Radius * _scale;
            Vector2 direction = ScreenDirection(spell.Direction);
            float angle = Mathf.Atan2(direction.y, direction.x);

            // 사각형 판정과 같은 회전을 쓴다. 발사는 항상 진행 방향, 범위·공전은 똑바로 세우기 설정을 따른다.
            float boxRotation = spell.Kind != SpellGrammar.FORM_BOLT && isAreaBoxUpright ? 0 : angle;
            if (spell.Kind == SpellGrammar.FORM_ZONE || spell.Kind == SpellGrammar.FORM_BURST)
                DrawAreaSpell(mesh, spell, point, radius, angle, boxRotation, tint);
            else
                DrawMovingSpell(mesh, spell, point, radius, direction, angle, boxRotation, tint);
        }

        /// <summary>폭발·잔류 범위를 사각형·부채꼴·원 판정 모양 그대로 채움과 외곽선으로 그린다. 예고 중인 잔류는 예고 표시로 그린다.</summary>
        private void DrawAreaSpell(UnityEngine.UI.VertexHelper mesh, SimulationSpellEntity spell, Vector2 point, float radius,
            float angle, float boxRotation, Color tint)
        {
            if (spell.IsWarning)
            {
                DrawWarningArea(mesh, spell, point, radius, angle, boxRotation);
                return;
            }
            Color fill = tint;
            fill.a = spell.Kind == SpellGrammar.FORM_ZONE ? 0.12f : 0.22f;
            if (spell.IsBox)
            {
                RuneMesh.Square(mesh, point, radius, boxRotation, fill);
                RuneMesh.SquareOutline(mesh, point, radius, boxRotation, 1.8f * _scale, tint);
                RuneMesh.SquareOutline(mesh, point, radius * 0.6f, boxRotation, _scale, fill * 2);
            }
            else if (spell.IsCone)
            {
                float coneAngle = (float)spell.ConeAngle * Mathf.Deg2Rad;
                RuneMesh.Sector(mesh, point, radius, angle, coneAngle, fill);
                RuneMesh.SectorOutline(mesh, point, radius, angle, coneAngle, 1.8f * _scale, tint);
            }
            else
            {
                RuneMesh.Polygon(mesh, point, radius, fill, spell.Element == "ice" ? 6 : 24);
                RuneMesh.Ring(mesh, point, radius, 1.8f * _scale, tint, spell.Element == "fire" ? 12 : 24);
                RuneMesh.Ring(mesh, point, radius * 0.6f, _scale, fill * 2);
            }
        }

        /// <summary>예고 중인 잔류 범위를 판정 모양 그대로 빨간 외곽선과 진행률만큼 커지는 반투명 채움으로 그린다.</summary>
        private void DrawWarningArea(UnityEngine.UI.VertexHelper mesh, SimulationSpellEntity spell, Vector2 point, float radius,
            float angle, float boxRotation)
        {
            Color fill = PERSIST_WARNING_COLOR;
            fill.a = 0.25f;
            float filled = radius * (float)spell.WarnProgress;
            if (spell.IsBox)
            {
                RuneMesh.Square(mesh, point, filled, boxRotation, fill);
                RuneMesh.SquareOutline(mesh, point, radius, boxRotation, 1.8f * _scale, PERSIST_WARNING_COLOR);
            }
            else if (spell.IsCone)
            {
                float coneAngle = (float)spell.ConeAngle * Mathf.Deg2Rad;
                RuneMesh.Sector(mesh, point, filled, angle, coneAngle, fill);
                RuneMesh.SectorOutline(mesh, point, radius, angle, coneAngle, 1.8f * _scale, PERSIST_WARNING_COLOR);
            }
            else
            {
                RuneMesh.Polygon(mesh, point, filled, fill, 24);
                RuneMesh.Ring(mesh, point, radius, 1.8f * _scale, PERSIST_WARNING_COLOR, 24);
            }
        }

        /// <summary>발사체·공전체를 꼬리선과 속성별 다각형(사각형 Shape면 회전 사각형)으로, 부채꼴 발사체는 꼬리선과 진행 방향 부채꼴로 그린다.</summary>
        private void DrawMovingSpell(UnityEngine.UI.VertexHelper mesh, SimulationSpellEntity spell, Vector2 point, float radius,
            Vector2 direction, float angle, float boxRotation, Color tint)
        {
            Color trail = tint;
            trail.a = 0.25f;
            RuneMesh.Line(mesh, point - direction * 24 * _scale, point, Math.Max(2, radius), trail);
            if (spell.IsCone)
            {
                Color fan = tint;
                fan.a = 0.6f;
                float coneAngle = (float)spell.ConeAngle * Mathf.Deg2Rad;
                float reach = (float)spell.ConeReach * _scale;
                RuneMesh.Sector(mesh, Point(spell.ConeApex), reach, angle, coneAngle, fan);
                RuneMesh.SectorOutline(mesh, Point(spell.ConeApex), reach, angle, coneAngle, _scale, tint);
                return;
            }
            float size = Math.Max(2.5f, radius);
            if (spell.IsBox)
            {
                RuneMesh.Square(mesh, point, size, boxRotation, tint);
                return;
            }
            RuneMesh.Polygon(mesh, point, size, tint, ElementSides(spell.Element), angle);
        }

        /// <summary>발사체 다각형의 속성별 변 수를 반환한다(화염 3, 얼음 6, 전격 4, 일반 16).</summary>
        private static int ElementSides(string element)
        {
            switch (element)
            {
                case "fire": return 3;
                case "ice": return 6;
                case "arc": return 4;
                default: return 16;
            }
        }

        /// <summary>적 탄환은 사각형과 꼬리선, 위험 장판은 예고 여부에 따른 반투명 원과 표식으로 그린다.</summary>
        private void DrawHostileProjectile(UnityEngine.UI.VertexHelper mesh, SimulationProjectile projectile)
        {
            Vector2 point = Point(projectile.Position);
            float radius = (float)projectile.Radius * _scale;
            if (projectile.IsHazard)
            {
                Color fill = HOSTILE_COLOR;
                fill.a = projectile.IsWarning ? 0.10f : 0.30f;
                RuneMesh.Polygon(mesh, point, radius, fill);
                RuneMesh.Ring(mesh, point, radius, 2 * _scale, HOSTILE_COLOR);
                RuneMesh.Line(mesh, point - Vector2.one * radius * 0.3f, point + Vector2.one * radius * 0.3f, 2 * _scale, HOSTILE_COLOR);
                return;
            }
            RuneMesh.Polygon(mesh, point, radius, HOSTILE_COLOR, 4);
            RuneMesh.Line(mesh, point - ScreenDirection(projectile.Direction) * 14 * _scale, point, radius, HOSTILE_TRAIL_COLOR);
        }

        /// <summary>처치 보상 조각을 마름모와 고리로 그린다.</summary>
        private void DrawOrb(UnityEngine.UI.VertexHelper mesh, FragmentOrb orb)
        {
            Vector2 point = Point(orb.Position);
            RuneMesh.Polygon(mesh, point, 6 * _scale, ORB_COLOR, 4);
            RuneMesh.Ring(mesh, point, 9 * _scale, _scale, ORB_RING_COLOR, 4);
        }

        /// <summary>적 종류에 따른 도형과 엘리트 링, 공격 예고·종류별 표시·상태 아이콘·체력 막대를 그린다.</summary>
        private void DrawEnemy(UnityEngine.UI.VertexHelper mesh, SimulationEnemy enemy, RuneSimulation sim)
        {
            Vector2 point = Point(enemy.Position);
            float radius = sim.Stage == MissionStage.Bench ? Mathf.Max(5, (float)enemy.Radius * _scale) : (float)enemy.Radius * _scale;
            Color tint = EnemyColor(enemy);
            int sides = EnemySides(enemy.Kind);
            float facing = Mathf.Atan2(-(float)enemy.Facing.Y, (float)enemy.Facing.X);
            RuneMesh.Polygon(mesh, point, radius, tint, sides, facing);
            RuneMesh.Polygon(mesh, point, radius * 0.48f, ENEMY_CORE_COLOR, sides, facing);
            // 엘리트는 강화형 금색, 신속형 청록색 링으로 구분한다. 두 배율을 모두 가지면 속도 색을 우선한다.
            if (enemy.IsElite) RuneMesh.Ring(mesh, point, radius + 5 * _scale, 2 * _scale, enemy.SpeedMultiplier > 1 ? ELITE_SPEED_COLOR : ELITE_HP_COLOR);
            if (enemy.IsWarning) RuneMesh.Ring(mesh, point, radius + (6 + Mathf.Sin(Time.unscaledTime * 16) * 3) * _scale, 2 * _scale, WARNING_COLOR);
            if (enemy.Kind == "enemy.relay") RuneMesh.Ring(mesh, point, GameData.Balance.Combat.RelayRadius * _scale, _scale, RELAY_AURA_COLOR);
            if (enemy.Kind == "enemy.aegis" && !sim.HasEnemyStatus(enemy, EnemyStatusType.Emp)) DrawAegisShield(mesh, point, radius, facing);
            if (enemy.IsPatching) RuneMesh.Ring(mesh, point, radius + 12 * _scale, 3 * _scale, PATCH_COLOR, 12);
            DrawEnemyStatuses(mesh, enemy, sim, point, radius);
            DrawEnemyHp(mesh, enemy, point, radius, tint);
        }

        /// <summary>적 종류·더미·피격 번쩍임에 맞는 몸체 색을 반환한다.</summary>
        private static Color EnemyColor(SimulationEnemy enemy)
        {
            if (enemy.IsFlashing) return Color.white;
            if (enemy.IsDummy) return DUMMY_COLOR;
            switch (enemy.Kind)
            {
                case "enemy.relay": return RELAY_COLOR;
                case "enemy.aegis": return AEGIS_COLOR;
                case "boss.governor": return BOSS_COLOR;
                default: return ENEMY_COLOR;
            }
        }

        /// <summary>적 종류별 몸체 다각형의 변 수를 반환한다.</summary>
        private static int EnemySides(string kind)
        {
            switch (kind)
            {
                case "enemy.scout": return 3;
                case "enemy.sentry":
                case "enemy.hunter": return 4;
                case "enemy.relay": return 8;
                default: return 6;
            }
        }

        /// <summary>이지스 적의 정면 방패 호(±60도)를 선분으로 그린다.</summary>
        private void DrawAegisShield(UnityEngine.UI.VertexHelper mesh, Vector2 point, float radius, float facing)
        {
            float distance = radius + 5 * _scale;
            for (int i = -5; i < 5; i++)
            {
                float from = facing + i * Mathf.PI / 15;
                float to = from + Mathf.PI / 15;
                RuneMesh.Line(mesh, point + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * distance,
                    point + new Vector2(Mathf.Cos(to), Mathf.Sin(to)) * distance, 3 * _scale, AEGIS_SHIELD_COLOR);
            }
        }

        /// <summary>적 머리 위에 화상·냉기(빙결)·EMP 아이콘과 적응 내성 표시를 그린다.</summary>
        private void DrawEnemyStatuses(UnityEngine.UI.VertexHelper mesh, SimulationEnemy enemy, RuneSimulation sim, Vector2 point, float radius)
        {
            float iconY = radius + 8 * _scale;
            float iconSize = 4 * _scale;
            if (sim.HasEnemyStatus(enemy, EnemyStatusType.Burn))
                RuneMesh.Polygon(mesh, point + new Vector2(-radius, iconY), iconSize, RuneMesh.ElementColor("fire"), 3);
            if (sim.GetChillStacks(enemy) > 0 || sim.HasEnemyStatus(enemy, EnemyStatusType.Freeze))
                RuneMesh.Polygon(mesh, point + new Vector2(0, iconY), iconSize, RuneMesh.ElementColor("ice"), 6);
            if (sim.HasEnemyStatus(enemy, EnemyStatusType.Emp))
                RuneMesh.Polygon(mesh, point + new Vector2(radius, iconY), iconSize, RuneMesh.ElementColor("arc"), 4);

            float threshold = GameData.Balance.Adaptation.ResistanceThreshold;
            bool isResistant = sim.Adaptation.Enabled
                && (sim.Adaptation.GetValue(enemy.LastDamageElement) >= threshold || sim.Adaptation.GetValue(enemy.LastDamageForm) >= threshold);
            if (isResistant) RuneMesh.Ring(mesh, point + new Vector2(radius + 8 * _scale, radius), iconSize, 1.5f * _scale, RESISTANT_COLOR, 6);
        }

        /// <summary>적 아래에 체력 막대(배경과 남은 비율)를 그린다.</summary>
        private void DrawEnemyHp(UnityEngine.UI.VertexHelper mesh, SimulationEnemy enemy, Vector2 point, float radius, Color tint)
        {
            float width = Math.Max(28, radius * 2);
            float top = point.y - radius - 8 * _scale;
            RuneMesh.Rect(mesh, new Rect(point.x - width / 2, top, width, 3 * _scale), HP_TRACK_COLOR);
            RuneMesh.Rect(mesh, new Rect(point.x - width / 2, top, width * (float)(enemy.Hp / enemy.MaxHp), 3 * _scale), tint);
        }

        /// <summary>플레이어를 조준 방향 삼각형·고리·조준선으로 그리고 대시 꼬리, 보호막, 화상 표시를 더한다.</summary>
        private void DrawPlayer(UnityEngine.UI.VertexHelper mesh, RuneSimulation sim)
        {
            SimulationPlayer player = sim.Player;
            bool isBench = sim.Stage == MissionStage.Bench;
            bool isDashing = player.DashRemaining > 0;
            Vector2 point = Point(player.Position);
            Vector2 aim = ScreenDirection(player.AimDirection);
            if (isDashing) RuneMesh.Line(mesh, point - aim * 44 * _scale, point, 14 * _scale, PLAYER_DASH_TRAIL_COLOR);

            float radius = isBench ? Mathf.Max(4.5f, 12 * _scale) : 12 * _scale;
            RuneMesh.Polygon(mesh, point, radius, isDashing ? PLAYER_DASH_COLOR : PLAYER_COLOR, 3, Mathf.Atan2(aim.y, aim.x));
            RuneMesh.Ring(mesh, point, 17 * _scale, _scale, PLAYER_RING_COLOR);
            float aimLength = Mathf.Max(isBench ? 8 : 0, 27 * _scale);
            float aimWidth = Mathf.Max(isBench ? 1 : 0, 3 * _scale);
            RuneMesh.Line(mesh, point, point + aim * aimLength, aimWidth, PLAYER_AIM_COLOR);
            if (player.Shield > 0) RuneMesh.Ring(mesh, point, 23 * _scale, 2 * _scale, SHIELD_COLOR);
            if (player.IsBurning(sim.Time)) RuneMesh.Polygon(mesh, point + new Vector2(0, 30 * _scale), 4 * _scale, RuneMesh.ElementColor("fire"), 3);
        }

        /// <summary>최근 피해 숫자를 위로 떠오르며 흐려지게 표시한다. 라벨은 재사용하고 남는 라벨은 숨긴다.</summary>
        private void UpdateDamageNumbers(RuneSimulation sim)
        {
            int visible = 0;
            foreach (DamageNumber number in sim.DamageNumbers)
            {
                int elapsed = sim.Tick - number.Tick;
                if (elapsed > DAMAGE_NUMBER_TICKS || visible >= MAX_DAMAGE_NUMBERS) continue;
                if (visible >= _damageLabels.Count) _damageLabels.Add(CreateDamageLabel());
                TextMeshProUGUI label = _damageLabels[visible++];
                label.gameObject.SetActive(true);
                label.rectTransform.anchoredPosition = Point(number.Position) + new Vector2(0, (12 + elapsed * 0.6f) * _scale);
                label.fontSize = Math.Max(8, 18 * _scale);
                Color tint = RuneMesh.ElementColor(number.Element);
                tint.a = 1 - elapsed / 42f;
                label.color = tint;
                label.text = number.Amount.ToString("0.#");
            }
            for (int i = visible; i < _damageLabels.Count; i++) _damageLabels[i].gameObject.SetActive(false);
        }

        /// <summary>경기장 자식으로 피해 숫자 라벨 하나를 만든다.</summary>
        private TextMeshProUGUI CreateDamageLabel()
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
            return text;
        }
    }
}
