using System;
using System.IO;
using UnityEngine;

namespace MoodPicker.MacAnchoring
{
    public sealed class MacAnchor
    {
        [Serializable]
        sealed class SavedAnchor
        {
            public int version;
            public MacScreenRectangle rectangle;
        }

        readonly MacAnchorSettings _settings;
        bool _hasSavedAnchor;

        public string FilePath { get; }
        public MacScreenRectangle Rectangle { get; private set; }
        public bool IsAvailable => _hasSavedAnchor && Rectangle.IsValid(_settings);

        public MacAnchor(string filePath = null, MacAnchorSettings settings = null)
        {
            _settings = settings ?? new MacAnchorSettings();
            FilePath = Path.GetFullPath(filePath ?? Path.Combine(Application.persistentDataPath, MacAnchorSettings.FileName));
            Load();
        }

        public bool Save(MacScreenRectangle rectangle)
        {
            if (!rectangle.IsValid(_settings)) return false;
            string temporaryPath = FilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(new SavedAnchor
                {
                    version = MacAnchorSettings.FormatVersion, rectangle = rectangle
                }, true));
                if (File.Exists(FilePath)) File.Replace(temporaryPath, FilePath, null);
                else File.Move(temporaryPath, FilePath);
                Rectangle = rectangle;
                _hasSavedAnchor = true;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        public bool Load()
        {
            _hasSavedAnchor = false;
            Rectangle = default;
            try
            {
                if (!File.Exists(FilePath)) return false;
                var saved = JsonUtility.FromJson<SavedAnchor>(File.ReadAllText(FilePath));
                if (saved == null || saved.version != MacAnchorSettings.FormatVersion ||
                    !saved.rectangle.IsValid(_settings)) return false;
                Rectangle = saved.rectangle;
                _hasSavedAnchor = true;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }
}
