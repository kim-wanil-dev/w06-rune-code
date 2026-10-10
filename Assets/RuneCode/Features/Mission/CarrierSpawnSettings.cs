using System;

using UnityEngine;

namespace RuneCode
{
    /// <summary>캐리어 Prefab의 생성 설정을 보관하고 미션·도크·CLI에 같은 읽기 전용 설정을 전달한다.</summary>
    public sealed class CarrierSpawnSettings : MonoBehaviour
    {
        private const string RESOURCE_PATH = "RuneCode/enemies/Carrier";

        [Header("생성 대상")]
        [SerializeField] private EnemyView[] _targets;

        [Header("생성 설정")]
        [SerializeField] private float _interval = 4;
        [SerializeField] private int _count = 2;
        [SerializeField] private float _distance = 120;
        [SerializeField] private int _maxAlive = 6;

        /// <summary>Prefab 대상의 적 ID와 생성 수치를 복사해 반환한다. 비어 있는 대상 참조는 데이터 오류로 보고한다.</summary>
        public CarrierDefinition CreateDefinition()
        {
            if (_targets == null || _targets.Length == 0) throw new FormatException("캐리어 생성 대상이 비어 있습니다.");
            var ids = new string[_targets.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (_targets[i] == null || string.IsNullOrEmpty(_targets[i].EnemyId)) throw new FormatException("캐리어 대상 Prefab의 적 ID 또는 참조가 없습니다.");
                ids[i] = _targets[i].EnemyId;
            }
            return new CarrierDefinition(ids, _interval, _count, _distance, _maxAlive);
        }

        /// <summary>Resources의 캐리어 Prefab 설정을 읽고 시뮬레이션용 값으로 반환한다. 자산이 없으면 연결 오류를 보고한다.</summary>
        public static CarrierDefinition LoadDefinition()
        {
            GameObject prefab = Resources.Load<GameObject>(RESOURCE_PATH);
            if (prefab == null || !prefab.TryGetComponent(out CarrierSpawnSettings settings))
                throw new InvalidOperationException("캐리어 Prefab 설정이 없습니다. Rune Code > Prepare Additional Enemies를 실행하세요.");
            return settings.CreateDefinition();
        }
    }
}
