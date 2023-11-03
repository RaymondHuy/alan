using Alan.WebApi.Models;
using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Alan.WebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AnalyzeController : ControllerBase
    {
        private readonly AzureDocumentSetting _azureDocumentSetting;
        private readonly AzureOpenAISetting _azureOpenAISetting;

        public AnalyzeController(AzureDocumentSetting azureDocumentSetting, AzureOpenAISetting azureOpenAISetting)
        {
            _azureDocumentSetting = azureDocumentSetting;
            _azureOpenAISetting = azureOpenAISetting;
        }

        [HttpPost]
        public async Task<AnalyzeResultResponse> Analyze([FromForm] AnalyzeResultRequest model)
        {
            List<TestResult> results = await ReadOCR(model);

            string askDefinitionQuestion = $"In medical context, can you help me explain what is {string.Join(',', results.Select(n => n.Name))}";

            Task<string> answerDefinitionQuestionTask = Answer(askDefinitionQuestion);

            string askAnalyzeQuestion = $"In medical context, can you help me analyze {string.Join(',', results.Select(n => $"value {n.Value} for {n.Name}"))}";

            Task<string> answerAnalyzeQuestionTask = Answer(askAnalyzeQuestion);

            string askRecommendQuestion = $"In medical context, can you help me how to improve {string.Join(',', results.Select(n => $"value {n.Value} for {n.Name}"))}";

            Task<string> answerRecommendQuestion = Answer(askRecommendQuestion);

            await Task.WhenAll(answerAnalyzeQuestionTask, answerDefinitionQuestionTask, answerRecommendQuestion);
            return new AnalyzeResultResponse()
            {
                Definition = answerDefinitionQuestionTask.Result,
                Analyze = answerAnalyzeQuestionTask.Result,
                Recommendation = answerRecommendQuestion.Result,
            };
        }

        private async Task<List<TestResult>> ReadOCR(AnalyzeResultRequest model)
        {
            var results = new List<TestResult>();
            AzureKeyCredential credential = new AzureKeyCredential(_azureDocumentSetting.Key);
            DocumentAnalysisClient client = new DocumentAnalysisClient(new Uri(_azureDocumentSetting.EndPoint), credential);

            using (var stream = model.File.OpenReadStream())
            {
                AnalyzeDocumentOperation operation = await client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-document", stream);

                AnalyzeResult result = operation.Value;

                for (int i = 0; i < result.Tables.Count; i++)
                {
                    DocumentTable table = result.Tables[i];
                    ExtractTableColumnResult extractInfo = Extract(table);

                    for (int rowNumber = 0; rowNumber < table.RowCount; rowNumber++)
                    {
                        List<DocumentTableCell> cells = table.Cells.Where(n => n.RowIndex == rowNumber).ToList();

                        if (cells.Count <= extractInfo.ValueIndex || cells.Count <= extractInfo.NameIndex)
                        {
                            continue;
                        }

                        if (decimal.TryParse(cells[extractInfo.ValueIndex].Content, out decimal bloodResult)
                            && !string.IsNullOrWhiteSpace(cells[extractInfo.NameIndex].Content))
                        {
                            results.Add(new TestResult()
                            {
                                Name = cells[extractInfo.NameIndex].Content,
                                Value = bloodResult,
                            });
                        }

                    }
                }
            }

            return results;
        }

        private async Task<string> Answer(string question)
        {
            OpenAIClient openAIClient = new OpenAIClient(
                new Uri(_azureOpenAISetting.EndPoint),
                new AzureKeyCredential(_azureOpenAISetting.Key));

            var messages = new List<ChatMessage>()
            {
                new ChatMessage()
                {
                    Content = question,
                    Role = ChatRole.User
                }
            };
            Response<ChatCompletions> response = await openAIClient.GetChatCompletionsAsync("alan", new ChatCompletionsOptions(messages));
            return response.Value.Choices.First().Message.Content;
        }

        private ExtractTableColumnResult Extract(DocumentTable table)
        {
            var valueIndexes = new List<int>();
            var unitIndexes = new List<int>();
            var rangeIndexes = new List<int>();
            var nameIndexes = new List<int>();
            for (int rowNumber = 0; rowNumber < table.RowCount; rowNumber++)
            {
                List<DocumentTableCell> cells = table.Cells.Where(n => n.RowIndex == rowNumber).ToList();

                for (int colNumber = 1; colNumber < cells.Count; colNumber++)
                {
                    string normalizedContent = cells[colNumber].Content.ToLower();
                    if (decimal.TryParse(cells[colNumber].Content, out _))
                    {
                        valueIndexes.Add(colNumber);
                    }
                    else if (cells[colNumber].Content.Contains('-'))
                    {
                        rangeIndexes.Add(colNumber);
                    }
                    else if (normalizedContent.Contains('/')
                        || normalizedContent.Contains("mg")
                        || normalizedContent.Contains("%")
                        || normalizedContent.Contains("ul")
                        || normalizedContent.Contains("mmol")
                        || normalizedContent.Contains("umol")
                        || normalizedContent.Contains("dl"))
                    {
                        unitIndexes.Add(colNumber);
                    }
                    else if (!string.IsNullOrWhiteSpace(normalizedContent))
                    {
                        nameIndexes.Add(colNumber);
                    }
                }
            }

            return new ExtractTableColumnResult()
            {
                NameIndex = nameIndexes.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key,
                ValueIndex = valueIndexes.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key,
                UnitIndex = unitIndexes.Any() ? unitIndexes.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key : null,
                RangeIndex = rangeIndexes.Any() ? rangeIndexes.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key : null,
            };
        }

        private class TestResult
        {
            public string Name { get; set; }
            public decimal Value { get; set; }
        }

        public class ExtractTableColumnResult
        {
            public int NameIndex { get; set; }

            public int ValueIndex { get; set; }

            public int? UnitIndex { get; set; }

            public int? RangeIndex { get; set; }
        }
    }
}
