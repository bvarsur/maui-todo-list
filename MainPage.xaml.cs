using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MauiTodoList.Resources.Models;

namespace MauiTodoList;

public partial class MainPage : ContentPage
{
    private const string TasksStorageKey = "Tasks";

    public ObservableCollection<TaskItem> PendingTasks { get; set; } = new();
    public ObservableCollection<TaskItem> CompletedTasks { get; set; } = new();

    public MainPage()
    {
        BindingContext = this;
        InitializeComponent();

        TodoListCollectionView.ItemsSource = PendingTasks;
        CompletedListCollectionView.ItemsSource = CompletedTasks;

        PendingTasks.CollectionChanged += (_, _) => UpdateEmptyMessages();
        CompletedTasks.CollectionChanged += (_, _) => UpdateEmptyMessages();

        LoadTasks();
        SetTodayDate();
        UpdateEmptyMessages();
    }

    private void SetTodayDate() =>
        TodayDateLabel.Text = DateTime.Now.ToString("dddd, MMMM dd, yyyy");

    private void LoadTasks()
    {
        if (!Preferences.ContainsKey(TasksStorageKey)) return;

        var serializedTasks = Preferences.Get(TasksStorageKey, string.Empty);
        if (string.IsNullOrWhiteSpace(serializedTasks)) return;

        var tasks = JsonSerializer.Deserialize<List<TaskItem>>(serializedTasks);
        if (tasks is null) return;

        foreach (var task in tasks)
        {
            (task.Completed ? CompletedTasks : PendingTasks).Add(task);
            task.PropertyChanged += Task_PropertyChanged;
        }
    }

    private void SaveTasks()
    {
        var allTasks = PendingTasks.Concat(CompletedTasks).ToList();
        var serializedTasks = JsonSerializer.Serialize(allTasks);
        Preferences.Set(TasksStorageKey, serializedTasks);
        UpdateEmptyMessages();
    }

    private void OnAddTaskClicked(object sender, EventArgs e)
    {
        var newTaskText = NewTaskEntry.Text?.Trim();

        if (string.IsNullOrEmpty(newTaskText))
        {
            ShowValidationError("Please enter a task here");
            return;
        }

        ClearValidationError();
        var newTask = new TaskItem { Text = newTaskText, Completed = false };
        newTask.PropertyChanged += Task_PropertyChanged;
        PendingTasks.Add(newTask);
        NewTaskEntry.Text = string.Empty;
        SaveTasks();
    }

    private void OnTaskCompleted(object sender, CheckedChangedEventArgs e)
    {
        var checkbox = (CheckBox)sender;
        var task = (TaskItem)checkbox.BindingContext;

        if (e.Value)
        {
            // Defer the modification to prevent modifying the collection during enumeration
            Dispatcher.Dispatch(() =>
            {
                if (PendingTasks.Contains(task))
                {
                    PendingTasks.Remove(task);
                    CompletedTasks.Add(task);
                }
            });
        }
        else
        {
            Dispatcher.Dispatch(() =>
            {
                if (CompletedTasks.Contains(task))
                {
                    CompletedTasks.Remove(task);
                    PendingTasks.Add(task);
                }
            });
        }
        SaveTasks();
    }

    private void Task_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TaskItem.Completed))
        {
            SaveTasks();
        }
    }

    private void OnDeleteTaskClicked(object sender, EventArgs e)
    {
        if (sender is not ImageButton button || button.CommandParameter is not TaskItem taskToDelete) return;

        taskToDelete.PropertyChanged -= Task_PropertyChanged;

        if (!PendingTasks.Remove(taskToDelete))
        {
            CompletedTasks.Remove(taskToDelete);
        }

        SaveTasks();
    }

    private async void ShowValidationError(string message)
    {
        ValidationMessage.Text = $"\u26A0\uFE0F {message}"; // ⚠ Warning emoji
        ValidationMessage.IsVisible = true;
        await Task.Delay(2500);
        ValidationMessage.IsVisible = false;
    }

    private void ClearValidationError() => ValidationMessage.IsVisible = false;

    private void UpdateEmptyMessages()
    {
        NoPendingTasksMessage.IsVisible = PendingTasks.Count == 0;
        NoCompletedTasksMessage.IsVisible = CompletedTasks.Count == 0;
    }
}