using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kanban.Editor
{
    public enum KanbanStatus { Backlog, Todo, Doing, Done }

    [Serializable]
    public class SubTask
    {
        public string text = "";
        public bool done;
    }

    [Serializable]
    public class LogEntry
    {
        public string timestamp;
        public string action;

        public LogEntry() { }
        public LogEntry(string action)
        {
            this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            this.action = action;
        }
    }

    [Serializable]
    public class KanbanTask
    {
        public string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        public string title = "New task";
        public string notes;
        public KanbanStatus status = KanbanStatus.Todo;
        public int priority = 2; // 0=critical, 1=high, 2=normal, 3=low
        public string tags;
        public string link;
        public bool archived;
        public string createdAt;
        public List<SubTask> subtasks = new();
        public List<LogEntry> log = new();

        public KanbanTask()
        {
            createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            log.Add(new LogEntry("Created"));
        }

        public void AddLog(string action)
        {
            log.Add(new LogEntry(action));
        }
    }

    [Serializable]
    public class KanbanBoardData
    {
        public List<KanbanTask> tasks = new();
    }

    // JSON-based storage for better git merging
    public static class KanbanStorage
    {
        private const string FilePath = "Assets/Editor/Kanban/kanban_board.json";

        public static KanbanBoardData Load()
        {
            if (!File.Exists(FilePath))
            {
                var newBoard = new KanbanBoardData();
                Save(newBoard);
                return newBoard;
            }

            try
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<KanbanBoardData>(json);
                return data ?? new KanbanBoardData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Kanban] Failed to load: {e.Message}");
                return new KanbanBoardData();
            }
        }

        public static void Save(KanbanBoardData data)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Pretty print JSON for better git diffs
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Kanban] Failed to save: {e.Message}");
            }
        }
    }
}
