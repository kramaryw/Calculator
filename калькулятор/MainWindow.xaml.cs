using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Packaging;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using калькулятор;
using Microsoft.Data.Sqlite;

namespace калькулятор

    {
        public partial class MainWindow : Window
        {
            private readonly Database _db = new Database();
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

            private string _input = "0";       // число, которое вводится сейчас
            private double? _left;             // первое число операции
            private string _op = "";           // ожидающая операция
            private string _expression = "";   // верхняя строка (например "5 +")
            private string _error = "";        // текст ошибки на дисплее
            private bool _newInput = true;     // следующая цифра начнёт новое число
            private bool _afterOp;             // только что нажали операцию
            private double _memory;
            private bool _hasMemory;

            public MainWindow()
            {
                InitializeComponent();

                // Покажет реальный текст любой непойманной ошибки вместо вылета
                Application.Current.DispatcherUnhandledException += (s, e) =>
                {
                    MessageBox.Show("Ошибка: " + e.Exception.Message);
                    e.Handled = true;
                };

                LoadHistory();
                UpdateDisplay();
            }

            // ---------- Вспомогательное ----------
            private double Cur =>
                double.TryParse(_input, NumberStyles.Float, Inv, out double v) ? v : 0;

            private static string Fmt(double d) => d.ToString("G15", Inv);

            private void UpdateDisplay()
            {
                if (txtResult == null || txtExpression == null || txtMemory == null) return;

                txtResult.Text = _error != "" ? _error : _input;
                txtExpression.Text = _expression;
                txtMemory.Text = _hasMemory ? "M" : "";
            }

            private void ClearAll()
            {
                _input = "0"; _left = null; _op = "";
                _expression = ""; _newInput = true; _afterOp = false;
            }

            private void ShowError(string message)
            {
                ClearAll();
                _error = message;
            }

            private static bool TryCalc(double a, string op, double b, out double r)
            {
                r = 0;
                switch (op)
                {
                    case "+": r = a + b; break;
                    case "-": r = a - b; break;
                    case "×": r = a * b; break;
                    case "÷":
                        if (b == 0) return false;
                        r = a / b; break;
                }
                return !double.IsInfinity(r) && !double.IsNaN(r);
            }

            private static string CalcError(string op, double b) =>
                op == "÷" && b == 0 ? "Деление на ноль" : "Переполнение";

            // ---------- Единая точка входа для кнопок и клавиатуры ----------
            private void Press(string c)
            {
                try
                {
                    _error = "";

                    if (c == "+" || c == "-" || c == "×" || c == "÷")
                    {
                        SetOperator(c);
                    }
                    else
                    {
                        _afterOp = false;
                        switch (c)
                        {
                            case "C": ClearAll(); break;
                            case "CE": _input = "0"; _newInput = false; break;
                            case "⌫": Backspace(); break;
                            case "=": Calculate(); break;
                            case "±": Negate(); break;
                            case "%": Percent(); break;
                            case "√":
                            case "x²":
                            case "1/x": Unary(c); break;
                            case "MC":
                            case "MR":
                            case "M+":
                            case "M-": Memory(c); break;
                            case ".": AddDot(); break;
                            default:
                                if (c.Length == 1 && c[0] >= '0' && c[0] <= '9') AddDigit(c);
                                break;
                        }

}
                }
                catch (Exception)
                {
                    ShowError("Ошибка");
                }
                UpdateDisplay();
            }

            private void Button_Click(object sender, RoutedEventArgs e)
            {
                Focus(); // чтобы клавиатура работала, даже если курсор был в поиске
                if (sender is Button btn)
                    Press(btn.Content?.ToString() ?? "");
            }

            // ---------- Ввод числа ----------
            private void AddDigit(string d)
            {
                if (_newInput) { _input = d; _newInput = false; }
                else if (_input == "0") _input = d;
                else if (_input.Length < 16) _input += d;
            }

            private void AddDot()
            {
                if (_newInput) { _input = "0."; _newInput = false; }
                else if (!_input.Contains(".")) _input += ".";
            }

            private void Backspace()
            {
                if (_newInput) return;
                if (_input.Length <= 1 || (_input.Length == 2 && _input[0] == '-'))
                    _input = "0";
                else
                    _input = _input.Substring(0, _input.Length - 1);
            }

            private void Negate()
            {
                if (_input == "0") return;
                _input = _input.StartsWith("-") ? _input.Substring(1) : "-" + _input;
            }

            // ---------- Операции ----------
            private void SetOperator(string op)
            {
                // Нажали операцию сразу после другой операции: просто меняем знак
                if (_afterOp)
                {
                    _op = op;
                    _expression = Fmt(_left ?? 0) + " " + op;
                    return;
                }

                double cur = Cur;

                // Цепочка: 2 + 3 × ... → сначала считаем 2 + 3
                if (_left.HasValue && _op != "")
                {
                    if (!TryCalc(_left.Value, _op, cur, out double r))
                    {
                        ShowError(CalcError(_op, cur));
                        return;
                    }
                    cur = r;
                    _input = Fmt(r);
                }

                _left = cur;
                _op = op;
                _expression = Fmt(cur) + " " + op;
                _newInput = true;
                _afterOp = true;
            }

            private void Calculate()
            {
                if (_op == "" || !_left.HasValue) return;

                double left = _left.Value;
                double right = Cur;

                if (!TryCalc(left, _op, right, out double r))
                {
                    ShowError(CalcError(_op, right));
                    return;
                }

                string expr = $"{Fmt(left)} {_op} {Fmt(right)}";
                _db.Add(expr, Fmt(r));          // запись в БД

                _input = Fmt(r);
                _expression = expr + " =";
                _left = null; _op = "";
                _newInput = true;
                LoadHistory();
            }

            private void Percent()
            {
                // как в Windows: 200 + 10 % → 20 (10% от первого числа)
                double v = _left.HasValue ? _left.Value * Cur / 100 : 0;
                _input = Fmt(v);
                _newInput = true;
            }

            private void Unary(string c)
            {
                double x = Cur, r;
                string expr;

                switch (c)
                {
                    case "√":
                        if (x < 0) { ShowError("Недопустимый ввод"); return; }
                        r = Math.Sqrt(x); expr = $"√({Fmt(x)})"; break;
                    case "x²":
                        r = x * x; expr = $"({Fmt(x)})²"; break;
                    default: // 1/x
                        if (x == 0) { ShowError("Деление на ноль"); return; }
                        r = 1 / x; expr = $"1/({Fmt(x)})"; break;
                }

                if (double.IsInfinity(r) || double.IsNaN(r)) { ShowError("Переполнение"); return; }

                _db.Add(expr, Fmt(r));
                _input = Fmt(r);
                _newInput = true;
                _expression = _op == "" ? expr + " =" : Fmt(_left ?? 0) + " " + _op;
                LoadHistory();
            }

            // ---------- Память ----------
            private void Memory(string c)
            {
                switch (c)


                {
                    case "MC": _memory = 0; _hasMemory = false; break;
                    case "MR":
                        if (_hasMemory) { _input = Fmt(_memory); _newInput = true; }
                        break;
                    case "M+": _memory += Cur; _hasMemory = true; _newInput = true; break;
                    case "M-": _memory -= Cur; _hasMemory = true; _newInput = true; break;
                }
            }

            // ---------- Клавиатура ----------
            private void Window_PreviewTextInput(object sender, TextCompositionEventArgs e)
            {
                if (txtSearch?.IsKeyboardFocused == true) return; // печатаем в поиск
                string t = e.Text ?? "";

                if (t.Length == 1 && t[0] >= '0' && t[0] <= '9') Press(t);
                else switch (t)
                    {
                        case "+": case "-": case "%": case "=": Press(t); break;
                        case "*": Press("×"); break;
                        case "/": Press("÷"); break;
                        case ".": case ",": Press("."); break;
                    }
            }

            private void Window_KeyDown(object sender, KeyEventArgs e)
            {
                if (txtSearch?.IsKeyboardFocused == true) return;
                switch (e.Key)
                {
                    case Key.Enter: Press("="); break;
                    case Key.Back: Press("⌫"); break;
                    case Key.Escape: Press("C"); break;
                    case Key.Delete: Press("CE"); break;
                }
            }

            // ---------- История ----------
            private void LoadHistory()
            {
                if (_db == null || lstHistory == null || txtSearch == null) return;
                lstHistory.ItemsSource = _db.GetAll(txtSearch.Text ?? "");
            }

            private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
            {
                if (!IsLoaded) return;
                LoadHistory();
            }

            // Клик по записи: вернуть результат на дисплей
            private void lstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (lstHistory?.SelectedItem is HistoryItem h)
                {
                    _error = "";
                    _input = h.Result;
                    _expression = h.Expression + " =";
                    _left = null; _op = "";
                    _newInput = true; _afterOp = false;
                    UpdateDisplay();
                    lstHistory.SelectedIndex = -1;
                    Focus();
                }
            }

            private void DeleteItem_Click(object sender, RoutedEventArgs e)
            {
                if (sender is Button btn && btn.Tag is int id)
                {
                    _db.Delete(id);
                    LoadHistory();
                }
            }

            private void ClearHistory_Click(object sender, RoutedEventArgs e)
            {
                _db.Clear();
                LoadHistory();
            }
        }
    }