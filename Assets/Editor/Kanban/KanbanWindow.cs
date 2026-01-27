using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kanban.Editor
{
    public class KanbanWindow : EditorWindow
    {
        private KanbanBoardData board;

        private Vector2[] scroll = new Vector2[4];
        private string quickTitle = "";
        private string search = "";
        private bool showArchive;

        private KanbanTask draggingTask;
        private KanbanStatus? hoverColumn;
        private KanbanTask selectedTask;

        // Colors
        private static readonly Color[] PriorityColors = {
            new Color(0.9f, 0.3f, 0.3f, 1f),  // Critical
            new Color(0.95f, 0.6f, 0.2f, 1f), // High
            new Color(0.3f, 0.7f, 0.9f, 1f),  // Normal
            new Color(0.5f, 0.5f, 0.5f, 1f),  // Low
        };

        private static readonly Color[] ColumnColors = {
            new Color(0.35f, 0.35f, 0.4f, 0.3f),
            new Color(0.4f, 0.5f, 0.6f, 0.3f),
            new Color(0.5f, 0.6f, 0.4f, 0.3f),
            new Color(0.3f, 0.5f, 0.3f, 0.3f),
        };

        [MenuItem("Tools/Kanban Board %#k")] // Ctrl+Shift+K / Cmd+Shift+K
        public static void Open() => GetWindow<KanbanWindow>("Kanban");

        private void OnEnable()
        {
            board = KanbanStorage.Load();
        }

        private void OnGUI()
        {
            if (board == null)
            {
                board = KanbanStorage.Load();
            }

            HandleHotkeys();
            DrawTopBar();
            EditorGUILayout.Space(4);

            if (showArchive)
            {
                DrawArchiveView();
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawColumn(KanbanStatus.Backlog, 0);
                    DrawColumn(KanbanStatus.Todo, 1);
                    DrawColumn(KanbanStatus.Doing, 2);
                    DrawColumn(KanbanStatus.Done, 3);
                }
            }

            HandleDragAndDrop();

            if (GUI.changed)
            {
                KanbanStorage.Save(board);
            }
        }

        private bool IsTextFieldFocused()
        {
            var focused = GUI.GetNameOfFocusedControl();
            return focused == "QuickAddField" || focused == "SearchField" || GUIUtility.keyboardControl != 0;
        }

        private void HandleHotkeys()
        {
            var evt = Event.current;
            if (evt.type != EventType.KeyDown) return;

            // Skip hotkeys when typing in text fields (except Escape)
            if (IsTextFieldFocused() && evt.keyCode != KeyCode.Escape)
            {
                return;
            }

            // Delete - delete selected (only with modifier to avoid accidents)
            if ((evt.keyCode == KeyCode.Delete) && selectedTask != null && (evt.control || evt.command))
            {
                if (EditorUtility.DisplayDialog("Delete", $"Delete \"{selectedTask.title}\"?", "Delete", "Cancel"))
                {
                    board.tasks.Remove(selectedTask);
                    selectedTask = null;
                    GUI.changed = true;
                }
                evt.Use();
            }
            // Ctrl+D - duplicate selected
            else if (evt.keyCode == KeyCode.D && (evt.control || evt.command) && selectedTask != null)
            {
                DuplicateTask(selectedTask);
                evt.Use();
            }
            // Arrow keys - move selected task
            else if (selectedTask != null && (evt.control || evt.command))
            {
                if (evt.keyCode == KeyCode.LeftArrow && selectedTask.status != KanbanStatus.Backlog)
                {
                    selectedTask.status = (KanbanStatus)((int)selectedTask.status - 1);
                    selectedTask.AddLog($"Moved to {selectedTask.status}");
                    GUI.changed = true;
                    evt.Use();
                }
                else if (evt.keyCode == KeyCode.RightArrow && selectedTask.status != KanbanStatus.Done)
                {
                    selectedTask.status = (KanbanStatus)((int)selectedTask.status + 1);
                    selectedTask.AddLog($"Moved to {selectedTask.status}");
                    GUI.changed = true;
                    evt.Use();
                }
            }
            // Ctrl+Shift+A - archive all done
            else if (evt.keyCode == KeyCode.A && (evt.control || evt.command) && evt.shift)
            {
                ArchiveAllDone();
                evt.Use();
            }
            // Escape - deselect and unfocus
            else if (evt.keyCode == KeyCode.Escape)
            {
                selectedTask = null;
                GUI.FocusControl(null);
                evt.Use();
            }
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                // Quick add
                GUILayout.Label("Add:", GUILayout.Width(28));
                GUI.SetNextControlName("QuickAddField");
                quickTitle = GUILayout.TextField(quickTitle, EditorStyles.toolbarTextField, GUILayout.Width(160));

                if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
                {
                    if (GUI.GetNameOfFocusedControl() == "QuickAddField")
                    {
                        AddQuickTask();
                        Event.current.Use();
                    }
                }

                if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(22)))
                    AddQuickTask(KanbanStatus.Todo);

                GUILayout.Space(8);

                // Archive toggle
                var archiveStyle = new GUIStyle(EditorStyles.toolbarButton);
                if (showArchive) archiveStyle.fontStyle = FontStyle.Bold;

                int archivedCount = board.tasks.Count(t => t.archived);
                if (GUILayout.Button($"📦 Archive ({archivedCount})", archiveStyle, GUILayout.Width(90)))
                {
                    showArchive = !showArchive;
                }

                // Archive all done
                int doneCount = board.tasks.Count(t => t.status == KanbanStatus.Done && !t.archived);
                GUI.enabled = doneCount > 0 && !showArchive;
                if (GUILayout.Button($"Archive Done ({doneCount})", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    ArchiveAllDone();
                }
                GUI.enabled = true;

                GUILayout.FlexibleSpace();

                // Search
                GUILayout.Label("🔍", GUILayout.Width(18));
                GUI.SetNextControlName("SearchField");
                search = GUILayout.TextField(search, EditorStyles.toolbarTextField, GUILayout.Width(120));
                if (!string.IsNullOrEmpty(search) && GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    search = "";
                    GUI.FocusControl(null);
                }

                GUILayout.Space(4);

                // Help
                if (GUILayout.Button("?", EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    ShowHotkeyHelp();
                }
            }
        }

        private void ShowHotkeyHelp()
        {
            EditorUtility.DisplayDialog("Hotkeys",
                "Enter - Add task (when typing in field)\n" +
                "Ctrl + ← → - Move selected task\n" +
                "Ctrl + D - Duplicate selected\n" +
                "Ctrl + Delete - Delete selected\n" +
                "Ctrl + Shift + A - Archive all done\n" +
                "Escape - Deselect / Unfocus\n" +
                "Ctrl + Shift + K - Open Kanban window",
                "OK");
        }

        private void AddQuickTask(KanbanStatus status = KanbanStatus.Todo)
        {
            if (string.IsNullOrWhiteSpace(quickTitle))
            {
                ShowNotification(new GUIContent("Enter a task title"), 1f);
                return;
            }

            var task = new KanbanTask
            {
                title = quickTitle.Trim(),
                status = status
            };
            board.tasks.Add(task);
            quickTitle = "";
            GUI.changed = true;
            GUI.FocusControl(null);
            Repaint();
        }

        private void DuplicateTask(KanbanTask source)
        {
            var copy = new KanbanTask
            {
                title = source.title + " (copy)",
                notes = source.notes,
                status = source.status,
                priority = source.priority,
                tags = source.tags,
                link = source.link,
                subtasks = source.subtasks.Select(s => new SubTask { text = s.text, done = false }).ToList()
            };
            copy.AddLog($"Duplicated from {source.id}");
            board.tasks.Add(copy);
            selectedTask = copy;
            GUI.changed = true;
            ShowNotification(new GUIContent("Task duplicated"), 1f);
        }

        private void ArchiveAllDone()
        {
            int count = 0;
            foreach (var task in board.tasks.Where(t => t.status == KanbanStatus.Done && !t.archived))
            {
                task.archived = true;
                task.AddLog("Archived");
                count++;
            }
            if (count > 0)
            {
                GUI.changed = true;
                ShowNotification(new GUIContent($"Archived {count} tasks"), 1.5f);
            }
        }

        private void DrawArchiveView()
        {
            var archived = board.tasks.Where(t => t.archived).Where(MatchesSearch).ToList();

            EditorGUILayout.LabelField($"📦 Archived Tasks ({archived.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (archived.Count == 0)
            {
                EditorGUILayout.HelpBox("No archived tasks.", MessageType.Info);
                return;
            }

            using (var scrollScope = new EditorGUILayout.ScrollViewScope(scroll[0]))
            {
                scroll[0] = scrollScope.scrollPosition;

                foreach (var task in archived.OrderByDescending(t => t.log.LastOrDefault()?.timestamp))
                {
                    using (new EditorGUILayout.HorizontalScope("box"))
                    {
                        EditorGUILayout.LabelField(task.title, GUILayout.Width(200));
                        EditorGUILayout.LabelField(task.status.ToString(), GUILayout.Width(60));
                        EditorGUILayout.LabelField(task.createdAt, GUILayout.Width(100));

                        if (GUILayout.Button("Restore", GUILayout.Width(60)))
                        {
                            task.archived = false;
                            task.AddLog("Restored from archive");
                            GUI.changed = true;
                        }
                        if (GUILayout.Button("Delete", GUILayout.Width(50)))
                        {
                            if (EditorUtility.DisplayDialog("Delete", $"Permanently delete \"{task.title}\"?", "Delete", "Cancel"))
                            {
                                board.tasks.Remove(task);
                                GUI.changed = true;
                            }
                        }
                    }
                }
            }
        }

        private void DrawColumn(KanbanStatus status, int idx)
        {
            float columnWidth = position.width / 4f - 6;

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(columnWidth)))
            {
                var tasks = board.tasks
                    .Where(t => t.status == status && !t.archived)
                    .Where(MatchesSearch)
                    .OrderBy(t => t.priority)
                    .ToList();

                // Header
                var headerText = $"{GetStatusIcon(status)} {status} ({tasks.Count})";
                var headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };

                var headerRect = GUILayoutUtility.GetRect(columnWidth, 24);
                var headerBg = ColumnColors[idx];
                headerBg.a = 0.6f;
                EditorGUI.DrawRect(headerRect, headerBg);
                GUI.Label(headerRect, headerText, headerStyle);

                // Body
                var dropRect = GUILayoutUtility.GetRect(columnWidth, position.height - 90,
                    GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

                var bgColor = ColumnColors[idx];
                if (hoverColumn == status && draggingTask != null)
                {
                    bgColor = new Color(0.4f, 0.7f, 0.4f, 0.4f);
                }
                EditorGUI.DrawRect(dropRect, bgColor);

                if (dropRect.Contains(Event.current.mousePosition))
                {
                    hoverColumn = status;
                }

                float contentHeight = GetContentHeight(tasks.Count);
                scroll[idx] = GUI.BeginScrollView(dropRect, scroll[idx],
                    new Rect(0, 0, dropRect.width - 16, contentHeight));

                float y = 4;
                foreach (var task in tasks)
                {
                    var cardRect = new Rect(4, y, dropRect.width - 24, GetCardHeight(task));
                    DrawCard(task, cardRect, status);
                    y += cardRect.height + 6;
                }

                GUI.EndScrollView();
            }
        }

        private string GetStatusIcon(KanbanStatus status) => status switch
        {
            KanbanStatus.Backlog => "📋",
            KanbanStatus.Todo => "📝",
            KanbanStatus.Doing => "🔨",
            KanbanStatus.Done => "✅",
            _ => ""
        };

        private float GetContentHeight(int count) => Mathf.Max(100, count * 100 + 10);

        private float GetCardHeight(KanbanTask task)
        {
            int subtaskCount = task.subtasks?.Count ?? 0;
            return 80 + (subtaskCount > 0 ? Mathf.Min(subtaskCount, 3) * 16 + 4 : 0);
        }

        private bool MatchesSearch(KanbanTask t)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var s = search.Trim().ToLowerInvariant();
            return (t.title ?? "").ToLowerInvariant().Contains(s)
                || (t.notes ?? "").ToLowerInvariant().Contains(s)
                || (t.tags ?? "").ToLowerInvariant().Contains(s)
                || (t.link ?? "").ToLowerInvariant().Contains(s);
        }

        private void DrawCard(KanbanTask task, Rect r, KanbanStatus currentColumn)
        {
            // Background
            var cardBg = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f, 1f)
                : new Color(0.95f, 0.95f, 0.95f, 1f);

            if (selectedTask == task)
            {
                cardBg = EditorGUIUtility.isProSkin
                    ? new Color(0.28f, 0.35f, 0.45f, 1f)
                    : new Color(0.8f, 0.88f, 0.95f, 1f);
            }
            else if (draggingTask == task)
            {
                cardBg = new Color(0.3f, 0.4f, 0.5f, 0.8f);
            }

            EditorGUI.DrawRect(r, cardBg);

            // Priority bar
            var priorityColor = PriorityColors[Mathf.Clamp(task.priority, 0, 3)];
            EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), priorityColor);

            // Title
            var titleRect = new Rect(r.x + 10, r.y + 4, r.width - 50, 18);
            var titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            GUI.Label(titleRect, TruncateString(task.title, 28), titleStyle);

            // Duplicate button
            if (GUI.Button(new Rect(r.x + r.width - 40, r.y + 4, 18, 16), "📋"))
            {
                DuplicateTask(task);
            }

            // Archive button
            if (GUI.Button(new Rect(r.x + r.width - 20, r.y + 4, 18, 16), "📦"))
            {
                task.archived = true;
                task.AddLog("Archived");
                GUI.changed = true;
            }

            // Tags
            float yOffset = 22;
            if (!string.IsNullOrWhiteSpace(task.tags))
            {
                var tagsRect = new Rect(r.x + 10, r.y + yOffset, r.width - 16, 14);
                var tagStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontStyle = FontStyle.Italic,
                    normal = { textColor = new Color(0.6f, 0.7f, 0.9f) }
                };
                GUI.Label(tagsRect, "🏷 " + task.tags, tagStyle);
                yOffset += 14;
            }

            // Subtasks preview
            if (task.subtasks != null && task.subtasks.Count > 0)
            {
                int done = task.subtasks.Count(s => s.done);
                var subStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = done == task.subtasks.Count ? new Color(0.4f, 0.8f, 0.4f) : Color.gray }
                };
                var subRect = new Rect(r.x + 10, r.y + yOffset, r.width - 16, 14);
                GUI.Label(subRect, $"☑ {done}/{task.subtasks.Count} subtasks", subStyle);
                yOffset += 14;
            }

            // Notes preview
            var notesRect = new Rect(r.x + 10, r.y + yOffset, r.width - 16, 16);
            var notesStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray }
            };
            var notesText = string.IsNullOrWhiteSpace(task.notes) ? "—" : TruncateString(task.notes, 40);
            GUI.Label(notesRect, notesText, notesStyle);

            // Buttons
            float btnY = r.y + r.height - 22;
            float btnW = (r.width - 20) / 4f;

            GUI.enabled = currentColumn != KanbanStatus.Backlog;
            if (GUI.Button(new Rect(r.x + 6, btnY, btnW - 2, 18), "◀"))
            {
                task.status = (KanbanStatus)((int)task.status - 1);
                task.AddLog($"Moved to {task.status}");
                GUI.changed = true;
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(r.x + 6 + btnW, btnY, btnW - 2, 18), "Edit"))
            {
                KanbanTaskEditor.Open(board, task);
            }

            GUI.enabled = !string.IsNullOrWhiteSpace(task.link);
            if (GUI.Button(new Rect(r.x + 6 + btnW * 2, btnY, btnW - 2, 18), "🔗"))
            {
                if (task.link.StartsWith("http")) Application.OpenURL(task.link);
            }
            GUI.enabled = true;

            GUI.enabled = currentColumn != KanbanStatus.Done;
            if (GUI.Button(new Rect(r.x + 6 + btnW * 3, btnY, btnW - 2, 18), "▶"))
            {
                task.status = (KanbanStatus)((int)task.status + 1);
                task.AddLog($"Moved to {task.status}");
                GUI.changed = true;
            }
            GUI.enabled = true;

            // Selection & drag
            var dragZone = new Rect(r.x, r.y, r.width, r.height - 24);
            if (Event.current.type == EventType.MouseDown && dragZone.Contains(Event.current.mousePosition))
            {
                selectedTask = task;

                if (Event.current.clickCount == 2)
                {
                    KanbanTaskEditor.Open(board, task);
                }
                else
                {
                    draggingTask = task;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.objectReferences = new Object[0];
                    DragAndDrop.SetGenericData("KANBAN_TASK", task);
                    DragAndDrop.StartDrag("📋 " + task.title);
                }
                Event.current.Use();
                Repaint();
            }
        }

        private string TruncateString(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= maxLen ? s : s.Substring(0, maxLen - 3) + "...";
        }

        private void HandleDragAndDrop()
        {
            var evt = Event.current;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                var task = DragAndDrop.GetGenericData("KANBAN_TASK") as KanbanTask;
                if (task == null) return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

                if (evt.type == EventType.DragPerform && hoverColumn.HasValue)
                {
                    DragAndDrop.AcceptDrag();
                    if (task.status != hoverColumn.Value)
                    {
                        task.status = hoverColumn.Value;
                        task.AddLog($"Moved to {task.status}");
                        GUI.changed = true;
                    }
                    draggingTask = null;
                    hoverColumn = null;
                }

                evt.Use();
                Repaint();
            }

            if (evt.type == EventType.DragExited || evt.type == EventType.MouseUp)
            {
                draggingTask = null;
                hoverColumn = null;
                Repaint();
            }
        }
    }

    // Task editor popup
    public class KanbanTaskEditor : EditorWindow
    {
        private KanbanBoardData board;
        private KanbanTask task;
        private Vector2 notesScroll;
        private Vector2 logScroll;
        private Vector2 subtaskScroll;
        private string newSubtask = "";
        private int selectedTab;

        private static readonly string[] PriorityLabels = { "🔴 Critical", "🟠 High", "🔵 Normal", "⚪ Low" };
        private static readonly string[] TabLabels = { "Details", "Subtasks", "History" };

        public static void Open(KanbanBoardData b, KanbanTask t)
        {
            var w = GetWindow<KanbanTaskEditor>(true, "Edit Task", true);
            w.board = b;
            w.task = t;
            w.minSize = new Vector2(420, 450);
            w.maxSize = new Vector2(600, 600);
            w.ShowUtility();
        }

        private void OnGUI()
        {
            if (board == null || task == null)
            {
                Close();
                return;
            }

            EditorGUILayout.Space(8);

            // Title
            EditorGUILayout.LabelField("Title", EditorStyles.boldLabel);
            var newTitle = EditorGUILayout.TextField(task.title);
            if (newTitle != task.title)
            {
                task.title = newTitle;
                task.AddLog("Title changed");
            }

            EditorGUILayout.Space(4);

            // Status & Priority
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
                    var newStatus = (KanbanStatus)EditorGUILayout.EnumPopup(task.status);
                    if (newStatus != task.status)
                    {
                        task.AddLog($"Status: {task.status} → {newStatus}");
                        task.status = newStatus;
                    }
                }
                GUILayout.Space(10);
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField("Priority", EditorStyles.boldLabel);
                    var newPriority = EditorGUILayout.Popup(task.priority, PriorityLabels);
                    if (newPriority != task.priority)
                    {
                        task.AddLog($"Priority: {task.priority} → {newPriority}");
                        task.priority = newPriority;
                    }
                }
            }

            EditorGUILayout.Space(8);

            // Tabs
            selectedTab = GUILayout.Toolbar(selectedTab, TabLabels);
            EditorGUILayout.Space(4);

            switch (selectedTab)
            {
                case 0: DrawDetailsTab(); break;
                case 1: DrawSubtasksTab(); break;
                case 2: DrawHistoryTab(); break;
            }

            EditorGUILayout.Space(8);

            // Bottom buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                var deleteStyle = new GUIStyle(GUI.skin.button);
                deleteStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);

                if (GUILayout.Button("Delete", deleteStyle, GUILayout.Width(70)))
                {
                    if (EditorUtility.DisplayDialog("Delete", $"Delete \"{task.title}\"?", "Delete", "Cancel"))
                    {
                        board.tasks.Remove(task);
                        KanbanStorage.Save(board);
                        Close();
                        return;
                    }
                }

                if (GUILayout.Button("Duplicate", GUILayout.Width(70)))
                {
                    var copy = new KanbanTask
                    {
                        title = task.title + " (copy)",
                        notes = task.notes,
                        status = task.status,
                        priority = task.priority,
                        tags = task.tags,
                        link = task.link,
                        subtasks = task.subtasks.Select(s => new SubTask { text = s.text, done = false }).ToList()
                    };
                    copy.AddLog($"Duplicated from {task.id}");
                    board.tasks.Add(copy);
                    KanbanStorage.Save(board);
                    task = copy;
                }

                if (GUILayout.Button(task.archived ? "Restore" : "Archive", GUILayout.Width(70)))
                {
                    task.archived = !task.archived;
                    task.AddLog(task.archived ? "Archived" : "Restored");
                    KanbanStorage.Save(board);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Save & Close", GUILayout.Width(100)))
                {
                    KanbanStorage.Save(board);
                    Close();
                }
            }

            if (GUI.changed)
            {
                KanbanStorage.Save(board);
            }
        }

        private void DrawDetailsTab()
        {
            // Tags
            EditorGUILayout.LabelField("Tags (comma separated)", EditorStyles.boldLabel);
            task.tags = EditorGUILayout.TextField(task.tags);

            EditorGUILayout.Space(4);

            // Link
            EditorGUILayout.LabelField("Link", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                task.link = EditorGUILayout.TextField(task.link);
                GUI.enabled = !string.IsNullOrWhiteSpace(task.link) && task.link.StartsWith("http");
                if (GUILayout.Button("Open", GUILayout.Width(50)))
                {
                    Application.OpenURL(task.link);
                }
                GUI.enabled = true;
            }

            EditorGUILayout.Space(4);

            // Notes
            EditorGUILayout.LabelField("Notes", EditorStyles.boldLabel);
            notesScroll = EditorGUILayout.BeginScrollView(notesScroll, GUILayout.Height(120));
            task.notes = EditorGUILayout.TextArea(task.notes, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            // Created
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"Created: {task.createdAt}", EditorStyles.miniLabel);
        }

        private void DrawSubtasksTab()
        {
            // Add new subtask
            using (new EditorGUILayout.HorizontalScope())
            {
                newSubtask = EditorGUILayout.TextField(newSubtask);
                if (GUILayout.Button("+", GUILayout.Width(30)) && !string.IsNullOrWhiteSpace(newSubtask))
                {
                    task.subtasks.Add(new SubTask { text = newSubtask.Trim() });
                    task.AddLog($"Added subtask: {newSubtask.Trim()}");
                    newSubtask = "";
                    GUI.changed = true;
                }
            }

            EditorGUILayout.Space(4);

            // Subtask list
            subtaskScroll = EditorGUILayout.BeginScrollView(subtaskScroll, GUILayout.Height(200));

            for (int i = 0; i < task.subtasks.Count; i++)
            {
                var sub = task.subtasks[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    var newDone = EditorGUILayout.Toggle(sub.done, GUILayout.Width(20));
                    if (newDone != sub.done)
                    {
                        sub.done = newDone;
                        task.AddLog($"Subtask '{sub.text}' {(sub.done ? "completed" : "uncompleted")}");
                    }

                    var style = new GUIStyle(EditorStyles.label);
                    if (sub.done) style.fontStyle = FontStyle.Italic;

                    EditorGUILayout.LabelField(sub.text, style);

                    if (GUILayout.Button("↑", GUILayout.Width(22)) && i > 0)
                    {
                        (task.subtasks[i], task.subtasks[i - 1]) = (task.subtasks[i - 1], task.subtasks[i]);
                        GUI.changed = true;
                    }
                    if (GUILayout.Button("↓", GUILayout.Width(22)) && i < task.subtasks.Count - 1)
                    {
                        (task.subtasks[i], task.subtasks[i + 1]) = (task.subtasks[i + 1], task.subtasks[i]);
                        GUI.changed = true;
                    }
                    if (GUILayout.Button("×", GUILayout.Width(22)))
                    {
                        task.AddLog($"Removed subtask: {sub.text}");
                        task.subtasks.RemoveAt(i);
                        GUI.changed = true;
                        break;
                    }
                }
            }

            EditorGUILayout.EndScrollView();

            // Summary
            if (task.subtasks.Count > 0)
            {
                int done = task.subtasks.Count(s => s.done);
                EditorGUILayout.LabelField($"Progress: {done}/{task.subtasks.Count} ({100 * done / task.subtasks.Count}%)", EditorStyles.boldLabel);
            }
        }

        private void DrawHistoryTab()
        {
            EditorGUILayout.LabelField("Activity Log", EditorStyles.boldLabel);

            logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(220));

            foreach (var entry in task.log.AsEnumerable().Reverse())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(entry.timestamp, GUILayout.Width(110));
                    EditorGUILayout.LabelField(entry.action);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
