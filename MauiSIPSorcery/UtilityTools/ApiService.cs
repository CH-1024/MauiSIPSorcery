using MauiSIPSorcery.Models;
using Microsoft.AspNetCore.SignalR.Client;
using SIPSorcery.Net;
using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MauiSIPSorcery.UtilityTools
{
    public class ApiService
    {
        const string baseURL = "http://hoyi.net.cn:8000";



        HubConnection _chatHub;
        HttpClient _httpClient;

        public event Action<string> SignalR_ReceiveIce;
        public event Action<string> SignalR_ReceiveOffer;
        public event Action<string> SignalR_ReceiveAnswer;


        public ApiService()
        {
            _httpClient = new HttpClient();

        }


        private void RegisterSignalREvent()
        {
            _chatHub.On<string>("ReceiveIce", ReceiveIce);
            _chatHub.On<string>("ReceiveOffer", ReceiveOffer);
            _chatHub.On<string>("ReceiveAnswer", ReceiveAnswer);
        }

        private void ReceiveIce(string ice)
        {
            SignalR_ReceiveIce?.Invoke(ice);
        }

        private void ReceiveOffer(string offer)
        {
            SignalR_ReceiveOffer?.Invoke(offer);
        }

        private void ReceiveAnswer(string answer)
        {
            SignalR_ReceiveAnswer?.Invoke(answer);
        }






        public async Task<ResponseModel> LoginAsync(string username, string password, CancellationToken cancellation = default)
        {
            var dic = new Dictionary<string, string>()
            {
                { "username", username },
                { "password", password },
            };

            var response = await HttpGetAsync("Login", dic, cancellation);
            if (response.IsSucc)
            {
                var token = response.GetValue<string>("Token");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                try
                {
                    _chatHub = new HubConnectionBuilder()
                    .WithUrl($"{baseURL}/chatHub", config => config.AccessTokenProvider = () => Task.FromResult(token))
                    .WithAutomaticReconnect().Build();

                    RegisterSignalREvent();

                    await _chatHub.StartAsync();
                }
                catch (Exception ex)
                {
                    return new ResponseModel
                    {
                        IsSucc = false,
                        Msg = ex.Message,
                        Data = null
                    };
                }
            }

            return response;
        }

        public async Task<ResponseModel> LoadContactListAsync(CancellationToken cancellation = default)
        {
            return await HttpGetAsync("LoadContactList", cancellationToken: cancellation);
        }



        public async Task SendIce(int targetId, string ice)
        {
            try
            {
                await _chatHub.SendAsync("SendIce", targetId, ice);
            }
            catch (Exception e)
            {
            }
        }

        public async Task SendOffer(int targetId, string offer)
        {
            try
            {
                await _chatHub.SendAsync("SendOffer", targetId, offer);
            }
            catch (Exception e)
            {
            }
        }

        public async Task SendAnswer(int targetId, string answer)
        {
            try
            {
                await _chatHub.SendAsync("SendAnswer", targetId, answer);
            }
            catch (Exception e)
            {
            }
        }





        private async Task<ResponseModel> HttpGetAsync(string str, Dictionary<string, string>? dic = null, CancellationToken cancellationToken = default)
        {
            try
            {
                dic ??= new Dictionary<string, string>();

                string queryString = string.Join("&", dic.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
                string url = $"{baseURL}/{str}?{queryString}";

                HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync() ?? string.Empty;
                return JsonSerializer.Deserialize<ResponseModel>(result) ?? new ResponseModel { };
            }
            catch (Exception e)
            {
                return new ResponseModel
                {
                    IsSucc = false,
                    Msg = e.Message,
                    Data = null
                };
            }
        }

    }
}
