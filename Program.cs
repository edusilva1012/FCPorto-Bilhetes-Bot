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
            string webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
            
            if (string.IsNullOrEmpty(webhookUrl))
            {
                Console.WriteLine("Erro: Webhook URL não encontrado.");
                return;
            }

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
            
            if (links == null)
            {
                Console.WriteLine("Não foram encontrados links na página.");
                return;
            }

            // Procura a palavra, mas ignora especificamente o link direto do menu da bilheteira
            var noticiaBilhetes = links.FirstOrDefault(a => 
                !string.IsNullOrWhiteSpace(a.InnerText) && 
                a.InnerText.Contains("bilhete", StringComparison.OrdinalIgnoreCase) &&
                !a.GetAttributeValue("href", "").Contains("bilhetes.fcporto.pt"));

            if (noticiaBilhetes != null)
            {
                string titulo = noticiaBilhetes.InnerText.Trim();
                titulo = Regex.Replace(titulo, @"\s+", " ");
                string safeTitle = titulo.Replace("\"", "\\\""); 
                
                string linkParcial = noticiaBilhetes.GetAttributeValue("href", "");
                string linkNoticia = linkParcial.StartsWith("http") ? linkParcial : $"https://www.fcporto.pt{linkParcial}";

                // O link direto de compra que gostas de ter à mão
                string linkCompra = "https://bilhetes.fcporto.pt/";

                string ultimoLinkAvisado = "";
                if (File.Exists(fileMemory))
                {
                    ultimoLinkAvisado = File.ReadAllText(fileMemory).Trim();
                }

                if (linkNoticia != ultimoLinkAvisado)
                {
                    Console.WriteLine($"Nova notícia encontrada: {titulo}");
                    
                    // O novo design da mensagem com os dois links separados
                    string jsonPayload = $@"{{
                        ""content"": ""🚨 **ALERTA BILHETES FC PORTO** 🚨\n\n📰 **Detalhes:** {safeTitle}\n🔗 **Ler Notícia:** {linkNoticia}\n🎫 **Comprar Diretamente:** {linkCompra}""
                    }}";

                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(webhookUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Mensagem enviada com sucesso!");
                        File.WriteAllText(fileMemory, linkNoticia); // Guarda apenas o link da notícia para evitar falsos positivos
                    }
                    else
                    {
                        Console.WriteLine($"Erro ao enviar para o Discord: {response.StatusCode}");
                    }
                }
                else
                {
                    Console.WriteLine("A notícia de bilhetes mais recente já foi notificada.");
                }
            }
            else
            {
                Console.WriteLine("Nenhuma notícia sobre bilhetes encontrada atualmente.");
            }
        }
    }
}