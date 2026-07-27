using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PschLib
{
    internal sealed class GoogleSheetWebClient
    {
        private readonly string _webAppUrl;

        public GoogleSheetWebClient(string webAppUrl)
        {
            if (string.IsNullOrWhiteSpace(webAppUrl))
            {
                throw new ArgumentException("The web app URL is empty.", nameof(webAppUrl));
            }

            _webAppUrl = webAppUrl.Trim();
        }

        public async Task<GoogleSheetListResponse> GetSheetListAsync()
        {
            var json = await GetJsonAsync(BuildUrl("action=list"));
            return Deserialize<GoogleSheetListResponse>(json);
        }

        public async Task<GoogleSheetDataResponse> GetSheetDataAsync(int sheetId)
        {
            var json = await GetJsonAsync(BuildUrl($"action=data&sheetId={sheetId}"));
            return Deserialize<GoogleSheetDataResponse>(json);
        }

        private string BuildUrl(string query)
        {
            var separator = _webAppUrl.Contains("?") ? "&" : "?";
            return $"{_webAppUrl}{separator}{query}";
        }

        private static Task<string> GetJsonAsync(string url)
        {
            var completion = new TaskCompletionSource<string>();
            var request = UnityWebRequest.Get(url);
            request.redirectLimit = 10;

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        completion.TrySetException(new InvalidOperationException($"Google Sheet request failed: {request.error}"));
                        return;
                    }

                    var json = request.downloadHandler.text;

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        completion.TrySetException(new InvalidOperationException("Google Sheet returned an empty response."));
                        return;
                    }

                    completion.TrySetResult(json);
                }
                finally
                {
                    request.Dispose();
                }
            };

            return completion.Task;
        }

        private static T Deserialize<T>(string json)
        {
            try
            {
                var response = JsonUtility.FromJson<T>(json);

                if (response == null)
                {
                    throw new InvalidOperationException("Google Sheet returned an invalid JSON response.");
                }

                return response;
            }
            catch (Exception exception) when (!(exception is InvalidOperationException))
            {
                throw new InvalidOperationException("Google Sheet response could not be parsed.", exception);
            }
        }
    }
}
