using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
// メモ：Explorerの要素(UIツリーの構造、ファイルのNameやControlType)を調査するにはAccessibility Insights for Windowsというツールを使うといいらしい。

namespace ExplorerItemScanner
{
    class Program
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;

        static string targetName = "";
        static string lastOutput = "";
        static readonly object lockObj = new object();
        static readonly HashSet<string> subscribedElements = new HashSet<string>();
        static readonly HashSet<string> subscribedWindows = new HashSet<string>();
        static System.Timers.Timer debounceTimer;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0)
            {
                Console.WriteLine("ERROR:引数がありません");
                return;
            }

            targetName = args[0];

            debounceTimer = new System.Timers.Timer(300); // 連続発火を防ぐため少し余裕を持たせる
            debounceTimer.AutoReset = false;
            debounceTimer.Elapsed += (s, e) => ScanAllExplorerWindows();

            try
            {
                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowOpenedEvent,
                    AutomationElement.RootElement,
                    TreeScope.Children,
                    OnWindowOpened);

                ScanAllExplorerWindows();
                SubscribeToExistingWindows();

                string line;
                while ((line = Console.ReadLine()) != "exit")
                {
                    if (line == null) break;
                }
            }
            catch (Exception ex)
            {
                // エラー時は標準エラー出力に流すか、形式を決めてUnityに伝える
                Console.WriteLine($"ERROR:メインループで例外発生 - {ex.Message}");
            }
            finally
            {
                Automation.RemoveAllEventHandlers();
            }
        }

        static void SubscribeToExistingWindows()
        {
            try
            {
                var explorerWindows = AutomationElement.RootElement.FindAll(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "CabinetWClass"));

                foreach (AutomationElement window in explorerWindows)
                {
                    SubscribeToWindowEvents(window);
                }
            }
            catch (ElementNotAvailableException) { /* 取得中に閉じられた場合は無視 */ }
        }

        static void OnWindowOpened(object sender, AutomationEventArgs e)
        {
            if (sender is AutomationElement element)
            {
                try
                {
                    if (element.Current.ClassName == "CabinetWClass")
                    {
                        SubscribeToWindowEvents(element);
                        RequestScan();
                    }
                }
                catch (ElementNotAvailableException) { }
            }
        }

        static void SubscribeToWindowEvents(AutomationElement window)
        {
            try
            {
                string id = GetRuntimeIdString(window);
                lock (lockObj)
                {
                    if (!subscribedWindows.Add(id)) return;
                }

                // フォルダ遷移やファイル増減の検知
                Automation.AddStructureChangedEventHandler(
                    window,
                    TreeScope.Descendants,
                    OnStructureChanged);

                // ウィンドウ自体の移動・リサイズを検知する
                Automation.AddAutomationPropertyChangedEventHandler(
                    window,
                    TreeScope.Element, // ウィンドウ自身のみを監視
                    OnWindowMoved,
                    AutomationElement.BoundingRectangleProperty);
            }
            catch (ElementNotAvailableException) { }
        }

        static void OnStructureChanged(object sender, StructureChangedEventArgs e)
        {
            RequestScan();
        }

        // ウィンドウの座標やサイズが変わった際に呼ばれる
        static void OnWindowMoved(object sender, AutomationPropertyChangedEventArgs e)
        {
            RequestScan();
        }

        static void RequestScan()
        {
            debounceTimer.Stop();
            debounceTimer.Start();
        }

        static void ScanAllExplorerWindows()
        {
            try
            {
                var explorerWindows = AutomationElement.RootElement.FindAll(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "CabinetWClass"));

                foreach (AutomationElement window in explorerWindows)
                {
                    ScanExplorerWindow(window);
                }
            }
            catch (ElementNotAvailableException) { }
        }

        static void ScanExplorerWindow(AutomationElement window)
        {
            try
            {
                // 【改善ポイント】ウィンドウ全体を探すのではなく、ファイルが格納されている「リスト」領域のみを探す
                Condition listCondition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataGrid) // 詳細表示の場合はDataGridになる
                );

                var listElements = window.FindAll(TreeScope.Descendants, listCondition);

                foreach (AutomationElement listElement in listElements)
                {
                    // 【改善ポイント】リスト領域が見つかったら、その直下（Children）からのみアイテムを探すため超高速
                    Condition nameCondition = new PropertyCondition(AutomationElement.NameProperty, targetName);
                    Condition typeCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem);
                    Condition andCondition = new AndCondition(nameCondition, typeCondition);

                    // リスト領域の「中」は Descendants で探す（グループ化の Group 要素を貫通するため）
                    AutomationElement targetElement = listElement.FindFirst(TreeScope.Descendants, andCondition)
                                                   ?? listElement.FindFirst(TreeScope.Descendants, nameCondition);

                    if (targetElement != null)
                    {
                        ReportElement(targetElement);
                        SubscribeToElementEvents(targetElement);
                        return; // このウィンドウでの目的のアイテムは見つかったので終了
                    }
                }
            }
            catch (ElementNotAvailableException) { }
        }

        static void ReportElement(AutomationElement element)
        {
            try
            {
                var rect = element.Current.BoundingRectangle;
                
                if (rect.IsEmpty || rect.Width == 0 || rect.Height == 0) return;

                int vScreenX = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int vScreenY = GetSystemMetrics(SM_YVIRTUALSCREEN);

                double relativeLeft = rect.Left - vScreenX;
                double relativeTop = rect.Top - vScreenY;

                string output = $"SUCCESS:{relativeLeft},{relativeTop},{rect.Width},{rect.Height}";
                
                lock (lockObj)
                {
                    if (output != lastOutput)
                    {
                        Console.WriteLine(output);
                        lastOutput = output;
                    }
                }
            }
            catch (ElementNotAvailableException) { }
        }

        static void SubscribeToElementEvents(AutomationElement element)
        {
            try
            {
                string id = GetRuntimeIdString(element);
                lock (lockObj)
                {
                    if (!subscribedElements.Add(id)) return;
                }

                Automation.AddAutomationPropertyChangedEventHandler(
                    element,
                    TreeScope.Element,
                    OnElementPropertyChanged,
                    AutomationElement.BoundingRectangleProperty);
            }
            catch (ElementNotAvailableException) { }
        }

        static void OnElementPropertyChanged(object sender, AutomationPropertyChangedEventArgs e)
        {
            if (sender is AutomationElement element)
            {
                ReportElement(element);
            }
        }

        static string GetRuntimeIdString(AutomationElement element)
        {
            int[] id = element.GetRuntimeId();
            return string.Join(",", id);
        }
    }
}