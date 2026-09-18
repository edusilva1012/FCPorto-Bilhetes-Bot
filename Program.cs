using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace FCPortoTicketsBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string webhooksEnv = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
            
            if (string.IsNullOrEmpty(webhooksEnv))
            {
                Console.WriteLine("Erro: Nenhuma Webhook URL encontrada.");
                return;
            }

            // Separa os links caso tenhas colocado vários divididos por vírgula
            string[] webhooks = webhooksEnv.Split(',', StringSplitOptions.RemoveEmptyEntries);

            string url = "https://www.fcporto.pt/pt/noticias";
            string fileMemory = "ultimo_aviso.txt";

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

            string html;
            try
            {
                html = await client.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao aceder ao site: {ex.Message}");
                return;
            }
            
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            if (links == null) return;

            var noticiaBilhetes = links.FirstOrDefault(a => 
                !string.IsNullOrWhiteSpace(a.InnerText) && 
                a.InnerText.Contains("bilhete", StringComparison.OrdinalIgnoreCase) &&
                !a.GetAttributeValue("href", "").Contains("bilhetes.fcporto.pt"));

            if (noticiaBilhetes != null)
            {
                string titulo = Regex.Replace(noticiaBilhetes.InnerText.Trim(), @"\s+", " ");
                string safeTitle = titulo.Replace("\"", "\\\""); 
                
                string linkParcial = noticiaBilhetes.GetAttributeValue("href", "");
                string linkNoticia = linkParcial.StartsWith("http") ? linkParcial : $"https://www.fcporto.pt{linkParcial}";
                string linkCompra = "https://bilhetes.fcporto.pt/";

                string ultimoLinkAvisado = File.Exists(fileMemory) ? File.ReadAllText(fileMemory).Trim() : "";

                if (linkNoticia != ultimoLinkAvisado)
                {
                    Console.WriteLine($"Nova notícia: {titulo}");
                    
                    string jsonPayload = $@"{{
                        ""content"": ""🚨 **ALERTA BILHETES FC PORTO** 🚨\n\n📰 **Detalhes:** {safeTitle}\n🔗 **Ler Notícia:** {linkNoticia}\n🎫 **Comprar Diretamente:** {linkCompra}""
                    }}";
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    // Envia a mensagem para cada Webhook na tua lista
                    foreach (var webhookUrl in webhooks)
                    {
                        var response = await client.PostAsync(webhookUrl.Trim(), content);
                        if (response.IsSuccessStatusCode)
                            Console.WriteLine("Mensagem enviada com sucesso para um servidor!");
                        else
                            Console.WriteLine($"Erro ao enviar: {response.StatusCode}");
                    }

                    File.WriteAllText(fileMemory, linkNoticia);
                }
                else
                {
                    Console.WriteLine("A notícia mais recente já foi notificada.");
                }
            }
        }
    }
}