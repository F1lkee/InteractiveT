using InteractiveT.Core.Enum;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace InteractiveT.Infrastructure.Services
{
    public class AIService
    {
        private readonly IConfiguration _config;
        private string _accessToken;
        private DateTime _tokenExpiry;

        public AIService(IConfiguration config)
        {
            _config = config;
        }

        private async Task EnsureTokenAsync()
        {
            if (_accessToken != null && DateTime.Now < _tokenExpiry)
                return;

            var clientId = _config["GigaChat:ClientId"];
            if (string.IsNullOrEmpty(clientId))
                throw new Exception("GigaChat ClientId не настроен в appsettings.json");

            var clientSecret = _config["GigaChat:ClientSecret"];
            if (string.IsNullOrEmpty(clientSecret))
                throw new Exception("GigaChat ClientSecret не настроен в appsettings.json");

            var authUrl = _config["GigaChat:AuthUrl"];
            if (string.IsNullOrEmpty(authUrl))
                authUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

            // Обход проблемы с самоподписанными сертификатами (для разработки)
            var handler = new System.Net.Http.HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = 
                System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            
            using (var authClient = new System.Net.Http.HttpClient(handler))
            {
                var credentials = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(string.Format("{0}:{1}", clientId, clientSecret)));

                var request = new HttpRequestMessage(HttpMethod.Post, authUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                request.Headers.Add("RqUID", Guid.NewGuid().ToString());
                request.Content = new StringContent(
                    "scope=GIGACHAT_API_PERS",
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded");

                HttpResponseMessage response;
                try
                {
                    response = await authClient.SendAsync(request);
                }
                catch (System.Net.Http.HttpRequestException httpEx)
                {
                    throw new Exception("Не удалось подключиться к серверу авторизации GigaChat. Проверьте интернет и настройки.\n\n" + httpEx.Message, httpEx);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(string.Format("Ошибка авторизации GigaChat ({0}): {1}", response.StatusCode, responseContent));
                }

                var tokenData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                _accessToken = tokenData.GetProperty("access_token").GetString();
                
                if (string.IsNullOrEmpty(_accessToken))
                    throw new Exception("Пустой access_token в ответе GigaChat. Ответ: " + responseContent);

                // expires_at может быть в секундах или миллисекундах
                long expiresAt;
                try
                {
                    expiresAt = tokenData.GetProperty("expires_at").GetInt64();
                }
                catch
                {
                    expiresAt = 0;
                }

                // Если expires_at > 10^12, это миллисекунды, иначе секунды
                if (expiresAt > 1000000000000L)
                    _tokenExpiry = DateTimeOffset.FromUnixTimeMilliseconds(expiresAt).LocalDateTime;
                else if (expiresAt > 0)
                    _tokenExpiry = DateTimeOffset.FromUnixTimeSeconds(expiresAt).LocalDateTime;
                else
                    _tokenExpiry = DateTime.Now.AddMinutes(30); // fallback 30 минут
            }
        }

        public async Task<List<GeneratedQuestion>> GenerateQuestionsAsync(
            string subject,
            string topic,
            int count,
            string difficulty)
        {
            await EnsureTokenAsync();

            var prompt = BuildPrompt(subject, topic, count, difficulty);

            var requestBody = new
            {
                model = "GigaChat-Pro",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "Ты - эксперт по составлению образовательных тестов. Отвечай строго в формате JSON без какого-либо дополнительного текста."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature = 0.7,
                max_tokens = 4000
            };

            var apiUrl = _config["GigaChat:ApiUrl"];
            if (string.IsNullOrEmpty(apiUrl))
                apiUrl = "https://gigachat.devices.sberbank.ru/api/v1";

            apiUrl = apiUrl + "/chat/completions";

            // Обход SSL проблем
            var handler = new System.Net.Http.HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = 
                System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            using (var apiClient = new System.Net.Http.HttpClient(handler))
            {
                var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                HttpResponseMessage response;
                try
                {
                    response = await apiClient.SendAsync(request);
                }
                catch (System.Net.Http.HttpRequestException httpEx)
                {
                    throw new Exception("Ошибка подключения к GigaChat API. Проверьте интернет.\n\n" + httpEx.Message, httpEx);
                }

                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(string.Format("GigaChat API вернул ошибку ({0}): {1}", response.StatusCode, responseContent));
                }

                var responseData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                string content;
                try
                {
                    content = responseData
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                }
                catch (Exception ex)
                {
                    throw new Exception("Не удалось разобрать ответ GigaChat. Сырой ответ: " + responseContent, ex);
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new Exception("GigaChat вернул пустой ответ.");
                }

                return ParseGeneratedQuestions(content);
            }
        }

        private string BuildPrompt(string subject, string topic, int count, string difficulty)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Создай " + count + " вопросов для школьного теста.");
            sb.AppendLine();
            sb.AppendLine("Предмет: " + subject);
            sb.AppendLine("Тема: " + topic);
            sb.AppendLine("Уровень сложности: " + difficulty);
            sb.AppendLine();
            sb.AppendLine("Требования:");
            sb.AppendLine("- Вопросы должны быть оригинальными, не копировать популярные интернет-источники");
            sb.AppendLine("- Формулировки понятные для школьников");
            sb.AppendLine("- Разнообразные формы вопросов");
            sb.AppendLine();
            sb.AppendLine("Формат ответа (строго JSON):");
            sb.AppendLine("{");
            sb.AppendLine("  \"questions\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"text\": \"текст вопроса\",");
            sb.AppendLine("      \"type\": \"single|multiple|text\",");
            sb.AppendLine("      \"points\": 1,");
            sb.AppendLine("      \"explanation\": \"подробное объяснение правильного ответа\",");
            sb.AppendLine("      \"answers\": [");
            sb.AppendLine("        {\"text\": \"вариант 1\", \"isCorrect\": true},");
            sb.AppendLine("        {\"text\": \"вариант 2\", \"isCorrect\": false},");
            sb.AppendLine("        {\"text\": \"вариант 3\", \"isCorrect\": false},");
            sb.AppendLine("        {\"text\": \"вариант 4\", \"isCorrect\": false}");
            sb.AppendLine("      ]");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("Для single: 4 варианта, 1 правильный");
            sb.AppendLine("Для multiple: 4-5 вариантов, несколько правильных");
            sb.AppendLine("Для text: пустой массив answers, правильный ответ в explanation");

            return sb.ToString();
        }

        private List<GeneratedQuestion> ParseGeneratedQuestions(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
                throw new Exception("Пустой ответ от GigaChat");

            try
            {
                // Очистка от markdown если есть
                jsonContent = jsonContent.Replace("```json", "").Replace("```", "").Trim();

                // Убираем возможные пробелы до начала JSON
                var jsonStart = jsonContent.IndexOf('{');
                if (jsonStart > 0)
                    jsonContent = jsonContent.Substring(jsonStart);

                var data = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                if (!data.TryGetProperty("questions", out var questions))
                {
                    throw new Exception("В ответе GigaChat отсутствует поле 'questions'. Содержимое: " + jsonContent);
                }

                var result = new List<GeneratedQuestion>();

                foreach (var q in questions.EnumerateArray())
                {
                    var question = new GeneratedQuestion
                    {
                        Text = q.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "",
                        Type = q.TryGetProperty("type", out var typeProp) ? ParseQuestionType(typeProp.GetString()) : QuestionType.SingleChoice,
                        Points = q.TryGetProperty("points", out var pointsProp) ? pointsProp.GetInt32() : 1,
                        Explanation = q.TryGetProperty("explanation", out var explProp) ? explProp.GetString() ?? "" : ""
                    };

                    if (q.TryGetProperty("answers", out var answers))
                    {
                        foreach (var a in answers.EnumerateArray())
                        {
                            question.Answers.Add(new GeneratedAnswer
                            {
                                Text = a.TryGetProperty("text", out var aText) ? aText.GetString() ?? "" : "",
                                IsCorrect = a.TryGetProperty("isCorrect", out var aCorrect) && aCorrect.GetBoolean()
                            });
                        }
                    }

                    result.Add(question);
                }

                return result;
            }
            catch (Exception ex) when (!(ex is ArgumentException))
            {
                throw new Exception(string.Format("Ошибка парсинга ответа GigaChat: {0}\n\nСодержимое: {1}", ex.Message, jsonContent.Length > 500 ? jsonContent.Substring(0, 500) + "..." : jsonContent));
            }
        }

        private QuestionType ParseQuestionType(string type)
        {
            if (string.IsNullOrEmpty(type))
                return QuestionType.SingleChoice;

            var lower = type.ToLower();
            if (lower == "multiple")
                return QuestionType.MultipleChoice;
            if (lower == "text")
                return QuestionType.TextInput;

            return QuestionType.SingleChoice;
        }
    }

    public class GeneratedQuestion
    {
        public string Text { get; set; }
        public QuestionType Type { get; set; }
        public int Points { get; set; }
        public string Explanation { get; set; }
        public List<GeneratedAnswer> Answers { get; set; }

        public GeneratedQuestion()
        {
            Text = "";
            Explanation = "";
            Answers = new List<GeneratedAnswer>();
        }
    }

    public class GeneratedAnswer
    {
        public string Text { get; set; }
        public bool IsCorrect { get; set; }

        public GeneratedAnswer()
        {
            Text = "";
        }
    }
}
