using InteractiveT.Core.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;

namespace InteractiveTWPF.Services
{
    public class ExcelExportService
    {
        /// <summary>
        /// Экспортирует результаты тестов в Excel файл
        /// </summary>
        /// <param name="attempts">Список попыток прохождения тестов</param>
        /// <param name="filePath">Путь для сохранения файла</param>
        public void ExportTestAttemptsToExcel(IEnumerable<TestAttempt> attempts, string filePath)
        {
            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (var package = new ExcelPackage())
                {
                    // Создаем лист с основными данными о попытках
                    var worksheet = package.Workbook.Worksheets.Add("Результаты тестов");

                    // Заголовки столбцов
                    worksheet.Cells[1, 1].Value = "Ученик";
                    worksheet.Cells[1, 2].Value = "Тест";
                    worksheet.Cells[1, 3].Value = "Предмет";
                    worksheet.Cells[1, 4].Value = "Дата начала";
                    worksheet.Cells[1, 5].Value = "Дата завершения";
                    worksheet.Cells[1, 6].Value = "Результат (%)";
                    worksheet.Cells[1, 7].Value = "Баллы";
                    worksheet.Cells[1, 8].Value = "Статус";
                    worksheet.Cells[1, 9].Value = "Время затрачено";

                    // Стилизация заголовков
                    using (var headerRange = worksheet.Cells[1, 1, 1, 9])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(200, 200, 200));
                        headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    // Заполнение данными
                    int row = 2;
                    foreach (var attempt in attempts)
                    {
                        worksheet.Cells[row, 1].Value = attempt.User?.FullName ?? "Неизвестно";
                        worksheet.Cells[row, 2].Value = attempt.Test?.Title ?? "Неизвестно";
                        worksheet.Cells[row, 3].Value = attempt.Test?.Subject?.Name ?? "Неизвестно";
                        worksheet.Cells[row, 4].Value = attempt.StartedAt.ToString("dd.MM.yyyy HH:mm");
                        worksheet.Cells[row, 5].Value = attempt.CompletedAt?.ToString("dd.MM.yyyy HH:mm") ?? "Не завершен";
                        
                        var percentage = attempt.MaxScore > 0 ? (attempt.Score / attempt.MaxScore * 100) : 0;
                        worksheet.Cells[row, 6].Value = Math.Round(percentage, 2);
                        worksheet.Cells[row, 6].Style.Numberformat.Format = "0.00";
                        
                        worksheet.Cells[row, 7].Value = $"{attempt.Score} из {attempt.MaxScore}";
                        worksheet.Cells[row, 8].Value = GetStatusText(attempt);
                        worksheet.Cells[row, 9].Value = attempt.TimeSpent?.ToString(@"hh\:mm\:ss") ?? "—";

                        // Цветовое кодирование статуса
                        if (attempt.IsCompleted)
                        {
                            if (percentage >= 80)
                                worksheet.Cells[row, 8].Style.Font.Color.SetColor(Color.Green);
                            else if (percentage >= 50)
                                worksheet.Cells[row, 8].Style.Font.Color.SetColor(Color.Orange);
                            else
                                worksheet.Cells[row, 8].Style.Font.Color.SetColor(Color.Red);
                        }

                        row++;
                    }

                    // Автоподбор ширины столбцов
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                    // Создаем лист с детальными ответами по каждому вопросу
                    var detailsWorksheet = package.Workbook.Worksheets.Add("Детальные ответы");

                    // Заголовки для детального листа
                    detailsWorksheet.Cells[1, 1].Value = "Ученик";
                    detailsWorksheet.Cells[1, 2].Value = "Тест";
                    detailsWorksheet.Cells[1, 3].Value = "Вопрос";
                    detailsWorksheet.Cells[1, 4].Value = "Тип вопроса";
                    detailsWorksheet.Cells[1, 5].Value = "Ответ ученика";
                    detailsWorksheet.Cells[1, 6].Value = "Правильный ответ";
                    detailsWorksheet.Cells[1, 7].Value = "Результат";
                    detailsWorksheet.Cells[1, 8].Value = "Баллы";
                    detailsWorksheet.Cells[1, 9].Value = "Время ответа";

                    // Стилизация заголовков
                    using (var headerRange = detailsWorksheet.Cells[1, 1, 1, 9])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(200, 200, 200));
                        headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                        headerRange.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    }

                    // Заполнение детальными данными
                    row = 2;
                    foreach (var attempt in attempts)
                    {
                        foreach (var answer in attempt.Answers.OrderBy(a => a.AnsweredAt))
                        {
                            detailsWorksheet.Cells[row, 1].Value = attempt.User?.FullName ?? "Неизвестно";
                            detailsWorksheet.Cells[row, 2].Value = attempt.Test?.Title ?? "Неизвестно";
                            detailsWorksheet.Cells[row, 3].Value = answer.Question?.Text ?? "Неизвестно";
                            detailsWorksheet.Cells[row, 4].Value = answer.Question?.Type.ToString() ?? "Неизвестно";
                            
                            // Получаем текст ответа ученика
                            string studentAnswerText = GetStudentAnswerText(answer);
                            detailsWorksheet.Cells[row, 5].Value = studentAnswerText;
                            
                            // Получаем правильные ответы
                            string correctAnswerText = GetCorrectAnswerText(answer.Question);
                            detailsWorksheet.Cells[row, 6].Value = correctAnswerText;
                            
                            detailsWorksheet.Cells[row, 7].Value = answer.IsCorrect ? "Верно" : "Неверно";
                            detailsWorksheet.Cells[row, 8].Value = answer.PointsEarned;
                            detailsWorksheet.Cells[row, 9].Value = answer.AnsweredAt.ToString("dd.MM.yyyy HH:mm:ss");

                            // Цветовое кодирование результата
                            if (answer.IsCorrect)
                                detailsWorksheet.Cells[row, 7].Style.Font.Color.SetColor(Color.Green);
                            else
                                detailsWorksheet.Cells[row, 7].Style.Font.Color.SetColor(Color.Red);

                            row++;
                        }
                    }

                    // Автоподбор ширины столбцов
                    detailsWorksheet.Cells[detailsWorksheet.Dimension.Address].AutoFitColumns();

                    // Сохраняем файл
                    var fileInfo = new FileInfo(filePath);
                    package.SaveAs(fileInfo);
                }

                MessageBox.Show($"Файл успешно сохранен:\n{filePath}", "Экспорт в Excel", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта в Excel: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetStatusText(TestAttempt attempt)
        {
            if (!attempt.IsCompleted)
                return "В процессе";
            
            if (attempt.IsPassed)
                return "Сдан";
            
            return "Не сдан";
        }

        private string GetStudentAnswerText(StudentAnswer answer)
        {
            if (answer.SelectedAnswer != null)
            {
                return answer.SelectedAnswer.Text;
            }
            
            if (!string.IsNullOrEmpty(answer.TextAnswer))
            {
                return answer.TextAnswer;
            }
            
            return "Нет ответа";
        }

        private string GetCorrectAnswerText(Question question)
        {
            if (question == null || question.Answers == null)
                return "Нет данных";

            var correctAnswers = question.Answers.Where(a => a.IsCorrect).Select(a => a.Text).ToList();
            
            if (correctAnswers.Count == 0)
                return "Нет правильных ответов";
            
            return string.Join("; ", correctAnswers);
        }

        /// <summary>
        /// Экспортирует результаты тестов в Excel файл с диалогом выбора пути
        /// </summary>
        /// <param name="attempts">Список попыток прохождения тестов</param>
        public void ExportTestAttemptsToExcelWithDialog(IEnumerable<TestAttempt> attempts)
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel файлы (*.xlsx)|*.xlsx|Все файлы (*.*)|*.*",
                DefaultExt = ".xlsx",
                FileName = $"Результаты_тестов_{DateTime.Now:yyyy-MM-dd_HH-mm}.xlsx"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                ExportTestAttemptsToExcel(attempts, saveFileDialog.FileName);
            }
        }
    }
}
