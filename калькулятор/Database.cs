using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;


        namespace калькулятор
        {
            public class HistoryItem
            {
                public int Id { get; set; }
                public string Expression { get; set; } = "";
                public string Result { get; set; } = "";
                public string CreatedAt { get; set; } = "";
            }

            public class Database
            {
                // Полный путь к файлу рядом с .exe, не зависит от рабочей папки
                private static readonly string ConnStr =
                    "Data Source=" + Path.Combine(AppContext.BaseDirectory, "calc.db");

                private bool _available;

                public Database()
                {
                    try
                    {
                        using var conn = new SqliteConnection(ConnStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = @"CREATE TABLE IF NOT EXISTS History(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Expression TEXT NOT NULL,
                    Result TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL)";
                        cmd.ExecuteNonQuery();
                        _available = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Не удалось открыть базу данных:\n" + ex.Message);
                    }
                }

                public void Add(string expression, string result)
                {
                    if (!_available) return;
                    try
                    {
                        using var conn = new SqliteConnection(ConnStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = "INSERT INTO History(Expression, Result, CreatedAt) VALUES($e,$r,$d)";
                        cmd.Parameters.AddWithValue("$e", expression);
                        cmd.Parameters.AddWithValue("$r", result);
                        cmd.Parameters.AddWithValue("$d", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка записи в БД:\n" + ex.Message);
                    }
                }

                public List<HistoryItem> GetAll(string filter = "")
                {
                    var list = new List<HistoryItem>();
                    if (!_available) return list;
                    try
                    {
                        using var conn = new SqliteConnection(ConnStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = @"SELECT Id, Expression, Result, CreatedAt FROM History
                                    WHERE Expression LIKE $f OR Result LIKE $f
                                    ORDER BY Id DESC";
                        cmd.Parameters.AddWithValue("$f", "%" + filter + "%");
                        using var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            list.Add(new HistoryItem
                            {
                                Id = reader.GetInt32(0),
                                Expression = reader.GetString(1),
                                Result = reader.GetString(2),
                                CreatedAt = reader.GetString(3)
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка чтения из БД:\n" + ex.Message);
                    }
                    return list;
                }

                public void Delete(int id)
                {
                    if (!_available) return;
                    try
                    {
                        using var conn = new SqliteConnection(ConnStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = "DELETE FROM History WHERE Id = $id";
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка удаления:\n" + ex.Message);
                    }
                }

                public void Clear()
                {
                    if (!_available) return;
                    try
                    {
                        using var conn = new SqliteConnection(ConnStr);
                        conn.Open();
                        var cmd = conn.CreateCommand();
                        cmd.CommandText = "DELETE FROM History";
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка очистки:\n" + ex.Message);
                    }
                }
            }
        }
    

