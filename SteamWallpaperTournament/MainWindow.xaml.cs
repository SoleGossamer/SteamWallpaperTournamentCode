using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WallpaperRanker
{
    public partial class MainWindow : Window
    {
        MediaElement? VideoLeft;
        MediaElement? VideoRight;
        public class WallpaperItem
        {
            public int Id { get; set; }
            public string Url { get; set; } = string.Empty;
            public int Score { get; set; }
        }

        List<WallpaperItem> allItems = new List<WallpaperItem>();
        HashSet<string> history = new HashSet<string>();
        WallpaperItem? leftNow, rightNow;
        Random rnd = new Random();

        string dbPath = "votes_data.txt";     // Сохранение баллов
        string historyPath = "history.txt";   // Сохранение пар

        public MainWindow()
        {
            InitializeComponent();
            InitializeTournament();
        }

        private async void OpenWallpapers_Click(object sender, RoutedEventArgs e)
        {
            string path = "wallpapers.txt";

            if (!File.Exists(path))
            {
                File.WriteAllText(path, "");
            }

            try
            {
                // Запуск процесса
                using (var process = System.Diagnostics.Process.Start("notepad.exe", path))
                {
                    if (process != null)
                    {
                        // Программа "засыпает" на этой строке, пока блокнот открыт.
                        // При этом интерфейс остается рабочим.
                        await process.WaitForExitAsync();

                        // Как только блокнот закрывается — происходит переинициализация турнира
                        InitializeTournament();

                        MessageBox.Show("Список обновлен!", "Инфо");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии файла: {ex.Message}");
            }
        }

        private void InitializeTournament()
        {
            if (!File.Exists("wallpapers.txt"))
            {
                MessageBox.Show("Файл wallpapers.txt не найден! Создайте его рядом с exe.");
                return;
            }

            var lines = File.ReadAllLines("wallpapers.txt")
                            .Where(l => !string.IsNullOrWhiteSpace(l))
                            .ToList();

            if (lines.Count < 2)
            {
                MessageBox.Show("Нужно минимум 2 ссылки в файле!");
                return;
            }

            allItems.Clear();
            for (int i = 0; i < lines.Count; i++)
                allItems.Add(new WallpaperItem { Id = i, Url = lines[i].Trim(), Score = 0 });

            // Загрузка баллов
            if (File.Exists(dbPath))
            {
                foreach (var s in File.ReadAllLines(dbPath))
                {
                    var parts = s.Split('|');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int val))
                    {
                        var item = allItems.FirstOrDefault(x => x.Id == id);
                        if (item != null) item.Score = val;
                    }
                }
            }

            // Загрузка истории
            if (File.Exists(historyPath))
            {
                var savedHistory = File.ReadAllLines(historyPath);
                history = new HashSet<string>(savedHistory);
            }

            // После загрузки истории, происходит подсчёт пар
            long n = allItems.Count;
            long maxPairs = n * (n - 1) / 2;

            ProgBar.Maximum = maxPairs;
            ProgBar.Value = history.Count;
            TxtStats.Text = $"Пара: {history.Count} / {maxPairs} | Элементов: {n}";

            NextMatch();
        }

        // Метод генерации ключа
        private string GetPairKey(string url1, string url2)
        {
            // Сортировка строк, чтобы (A, B) и (B, A) давали один ключ
            return string.Compare(url1, url2) < 0 ? $"{url1}|{url2}" : $"{url2}|{url1}";
        }
        private void NextMatch()
        {
            int n = allItems.Count;
            if (n < 2) return;

            long maxPairs = (long)n * (n - 1) / 2;

            // Очистка истории от пар, где один из элементов был удален (на всякий случай)
            // Это позволит счетчику пар всегда быть актуальным

            bool found = false;
            int attempts = 0;
            while (!found && attempts < 500)
            {
                var pair = allItems.OrderBy(x => rnd.Next()).Take(2).ToList();
                string key = GetPairKey(pair[0].Url, pair[1].Url);

                if (!history.Contains(key))
                {
                    leftNow = pair[0];
                    rightNow = pair[1];
                    found = true;
                }
                attempts++;
            }

            // Если рандом не нашел, идет метод перебора
            if (!found)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        string key = GetPairKey(allItems[i].Url, allItems[j].Url);
                        if (!history.Contains(key))
                        {
                            leftNow = allItems[i]; rightNow = allItems[j];
                            found = true; break;
                        }
                    }
                    if (found) break;
                }
            }

            if (found)
            {
                // Инициализация ссылок на плееры, если они еще не найдены
                // (Нахождение их по x:Name внутри шаблона кнопок)
                if (VideoLeft == null) VideoLeft = GetMediaFromButton(BtnLeftContainer, "VideoLeftInside");
                if (VideoRight == null) VideoRight = GetMediaFromButton(BtnRightContainer, "VideoRightInside");

                if (VideoLeft != null && VideoRight != null)
                {
                    VideoLeft.Source = new Uri(leftNow!.Url);
                    VideoRight.Source = new Uri(rightNow!.Url);
                    VideoLeft.Play();
                    VideoRight.Play();
                }
                // Обновление статистики
                n = allItems.Count;
                maxPairs = (long)n * (n - 1) / 2;

                TxtStats.Text = $"Пара: {history.Count + 1} / {maxPairs} | Элементов: {n}";
                ProgBar.Maximum = maxPairs;
                ProgBar.Value = history.Count;
            }
            else
            {
                MessageBox.Show("Все доступные пары сравнены!");
            }
        }

        private void RemoveWallpaper(WallpaperItem? itemToRemove)
        {
            if (itemToRemove == null) return;

            if (MessageBox.Show("Удалить эти обои навсегда?", "Удаление", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                string removedUrl = itemToRemove.Url;

                // Удаление самого элемента из списка
                allItems.Remove(itemToRemove);

                // Удаление из истории всех записей, содержащих этот URL
                // Это восстановит количество доступных пар корректно
                history.RemoveWhere(key => key.Contains(removedUrl));

                // Сохранение изменений в файлы
                File.WriteAllLines("wallpapers.txt", allItems.Select(x => x.Url));
                SaveData();

                NextMatch();
            }
        }

        private void BtnLeft_Click(object sender, RoutedEventArgs e)
        {
            if (leftNow != null) Vote(leftNow);
        }

        private void BtnRight_Click(object sender, RoutedEventArgs e)
        {
            if (rightNow != null) Vote(rightNow);
        }
        private void BtnDeleteLeft_Click(object sender, RoutedEventArgs e)
        {
            RemoveWallpaper(leftNow);
        }

        private void BtnDeleteRight_Click(object sender, RoutedEventArgs e)
        {
            RemoveWallpaper(rightNow);
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e) { NextMatch(); }

        private void Vote(WallpaperItem winner)
        {
            winner.Score++;
            history.Add(GetPairKey(leftNow!.Url, rightNow!.Url));
            SaveData();
            NextMatch();
        }

        private void SaveData()
        {
            // Сохранение баллов
            File.WriteAllLines(dbPath, allItems.Select(x => $"{x.Id}|{x.Score}"));
            // Сохранение истории
            File.WriteAllLines(historyPath, history);
            // Сохранение читаемого ТОП
            var top = allItems.OrderByDescending(x => x.Score).Select(x => $"{x.Score} б. - {x.Url}");
            File.WriteAllLines("Tournament_Results.txt", top);
        }

        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Использование паттерн-матчинга: если sender это MediaElement, 
            // то созда'тся' переменная 'me' и выполняется код.
            if (sender is MediaElement me)
            {
                me.Position = TimeSpan.FromMilliseconds(1);
                me.Play();
            }
        }

        private void ShowTop_Click(object sender, RoutedEventArgs e)
        {
            SaveData();
            System.Diagnostics.Process.Start("notepad.exe", "Tournament_Results.txt");
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Удалить весь прогресс?", "Сброс", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                // 1. Удаление файлов данных
                if (File.Exists(dbPath)) File.Delete(dbPath);
                if (File.Exists(historyPath)) File.Delete(historyPath);

                // 2. Получение пути к текущему процессу
                var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                string exePath = currentProcess.MainModule?.FileName ?? AppDomain.CurrentDomain.BaseDirectory;

                // 3. Запуск новой копии и закрытие текущей
                System.Diagnostics.Process.Start(exePath);
                Application.Current.Shutdown();
            }
        }

        // Метод для поиска MediaElement внутри шаблона кнопки
        private MediaElement? GetMediaFromButton(Button btn, string name)
        {
            btn.ApplyTemplate();
            var border = System.Windows.Media.VisualTreeHelper.GetChild(btn, 0) as FrameworkElement;
            return border?.FindName(name) as MediaElement;
        }
    }
}