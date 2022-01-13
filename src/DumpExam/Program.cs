using Markdig;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DumpExam
{
    class Program
    {
        class Issue
        {
            public string url { get; set; }
            public string html_url { get; set; }
            public int number { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
            public string body { get; set; }
        }

        class QueryResponse
        {
            public Issue[] items { get; set; }
        }

        static void Main(string[] args)
        {
            var currentDir = Directory.GetCurrentDirectory();
            Console.WriteLine(string.Format("Current working directory '{0}'", currentDir));

            Az204();
        }

        static void Az204()
        {
            BuildWebContent("AZ-204");           
        }

        static string BuildQuery(string examLabel)
        {
            return string.Format("https://api.github.com/search/issues?q=author:VanDng+label:ExamQuestion+label:{0}+repo:VanDng/DumpExam&sort=created&order=asc", examLabel);
        }

        static void BuildWebContent(string examLabel)
        {
            Console.WriteLine(string.Format("Exam: {0}", examLabel));

            var query = BuildQuery(examLabel);

            var queryResponse = GetIssues(query).Result;

            GenerateHtml(queryResponse.items, examLabel);
        }

        static async Task<QueryResponse> GetIssues(string query)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("request");

            var response = await httpClient.GetAsync(query);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var queryResponse = JsonSerializer.Deserialize<QueryResponse>(responseString);

                Console.WriteLine(string.Format("Github query item count: {0}", queryResponse.items.Length));

                return queryResponse;
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        static void GenerateHtml(Issue[] issues, string exam)
        {
            var docsDir          = Path.Combine("..", "..", "..", "..", "..", "docs");
            var questionsDir     = Path.Combine(docsDir, exam);
            var questionTemplate = File.ReadAllText(Path.Combine(docsDir, "question_template.html"));

            if (Directory.Exists(questionsDir))
            {
                Console.WriteLine(string.Format("Delete folder '{0}'", questionsDir));
                Directory.Delete(questionsDir, true);
                Thread.Sleep(1000);
            }

            Console.WriteLine(string.Format("Create folder '{0}'", questionsDir));
            Directory.CreateDirectory(questionsDir);
            Thread.Sleep(1000);

            Console.WriteLine(string.Format("There will be {0} questions", issues.Length));

            for (int issueIdx = 0; issueIdx < issues.Length; issueIdx++)
            {
                var issue = issues[issueIdx];

                //
                // Question ID
                //

                var previousQuestionId = issue.number;
                var nextQuestionId     = issue.number;
                var currentQuestionId  = issue.number;

                if (issueIdx > 0)
                {
                    previousQuestionId = currentQuestionId - 1;
                }

                if (issueIdx < issues.Length -1)
                {
                    nextQuestionId = currentQuestionId + 1;
                }

                //
                // Question/Answer content
                //

                // Index
                var answerKey      = "[Answer]";
                var answerIdxBegin = issue.body.IndexOf(answerKey) + answerKey.Length;
                var answerIdxEnd   = issue.body.Length - 1;

                var questionKey      = "[Question]";
                var questionIdxBegin = issue.body.IndexOf(questionKey) + questionKey.Length;
                var questionIdxEnd   = answerIdxBegin - answerKey.Length;

                // Question
                var questionMardown = issue.body.Substring(questionIdxBegin, questionIdxEnd - questionIdxBegin);
                var questionContent = Markdown.ToHtml(questionMardown);

                // Answer
                var answerMardown = issue.body.Substring(answerIdxBegin, answerIdxEnd - answerIdxBegin);
                var answerContent = Markdown.ToHtml(answerMardown);

                //
                // Title
                //
                var title = string.Format("{0}#{1}", exam.ToUpper(), currentQuestionId);

                //
                // Complete question content
                //

                var completeQuestionContent = questionTemplate.Replace("[QuestionContent]", questionContent)
                                                              .Replace("[AnswerContent]", answerContent)
                                                              .Replace("[PreviousQuestionId]", previousQuestionId.ToString())
                                                              .Replace("[CurrentQuestionId]", currentQuestionId.ToString())
                                                              .Replace("[NextQuestionId]", nextQuestionId.ToString())
                                                              .Replace("[GithubUrl]", issue.html_url)
                                                              .Replace("[Title]", title)
                                                              .Replace("[TotalQuestionCount]", issues.Length.ToString());

                var questionFilePath = string.Format("{0}.html", Path.Combine(questionsDir, currentQuestionId.ToString()));

                File.WriteAllText(questionFilePath, completeQuestionContent);

                Console.WriteLine(string.Format("{0}: Question #{1} created", exam, currentQuestionId));
            }
        }
    }
}
