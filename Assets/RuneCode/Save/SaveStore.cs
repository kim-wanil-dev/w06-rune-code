using System;
using System.IO;

using UnityEngine;

namespace RuneCode
{
    public static class SaveStore
    {
        private const string SAVE_FILE = "runecode.save.v3.json";
        private const string PREVIOUS_SAVE_FILE = "runecode.save.v2.json";
        private const string LEGACY_SAVE_FILE = "runecode.save.v1.json";
        private static string _lastWarning;
        public static string LastWarning => _lastWarning;
        public static string SavePath => Path.Combine(Application.persistentDataPath, SAVE_FILE);

        /// <summary>버전 3 진행을 읽거나 v1·v2 원본을 보존하여 이전하고 손상된 파일은 백업한다.</summary>
        public static PlayerSave Load()
        {
            _lastWarning = null;
            var previousPath = Path.Combine(Application.persistentDataPath, PREVIOUS_SAVE_FILE);
            var sourcePath = File.Exists(SavePath) ? SavePath : File.Exists(previousPath) ? previousPath : Path.Combine(Application.persistentDataPath, LEGACY_SAVE_FILE);
            if (!File.Exists(sourcePath)) return PlayerSave.CreateNew();
            try
            {
                var raw = File.ReadAllText(sourcePath);
                var save = Migrate(JsonUtility.FromJson<PlayerSave>(raw));
                if (save != null && save.Validate(out _)) return save;
                _lastWarning = GameData.L("save.recovered");
                File.Copy(sourcePath, sourcePath + ".invalid-backup", true);
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is UnauthorizedAccessException || exception is FormatException)
            {
                _lastWarning = GameData.L("save.unavailable");
                if (exception is FormatException || exception is ArgumentException)
                {
                    try { File.Copy(sourcePath, sourcePath + ".invalid-backup", true); }
                    catch (Exception copyException) when (copyException is IOException || copyException is UnauthorizedAccessException) { }
                }
            }
            return PlayerSave.CreateNew();
        }

        /// <summary>기존 v1·v2 설계와 진행을 버전 3으로 이전하고 알 수 없는 버전은 원본을 보존한다.</summary>
        private static PlayerSave Migrate(PlayerSave save)
        {
            if (save == null || save.Version == 3) return save;
            if (save.Version == 1) save.MigrateToIncremental();
            if (save.Version == 2) { save.MigrateToModular(); return save; }
            throw new FormatException("지원하지 않는 룬 코드 저장 버전입니다.");
        }

        /// <summary>진행을 임시 파일에 기록한 뒤 이전 정상 파일 백업과 함께 교체한다.</summary>
        public static bool Write(PlayerSave save)
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                var temporary = SavePath + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(save, true));
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, SavePath + ".backup");
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                _lastWarning = GameData.L("save.unavailable");
                return false;
            }
        }
    }
}
