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
            // O GitHub Actions vai injetar o link secret aqui.
            // Para testares no teu PC, podes colar o teu link do Discord diretamente entre as aspas na linha abaixo.
            string webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
            
            if (string.IsNullOrEmpty(webhookUrl))
            {
                Console.WriteLine("Erro: Webhook URL não encontrado.");
                return;
            }

            string url = "https://www.fcporto.pt/pt/noticias";
            string fileMemory = "ultimo_aviso.txt";

            using HttpClient client = new HttpClient();
            // Disfarça o bot como um browser normal para o site do FC Porto não bloquear o acesso
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

            // Puxa todos os links da página
            var links = doc.DocumentNode.SelectNodes("//a[@href]");
            
            if (links == null)
            {
                Console.WriteLine("Não foram encontrados links na página.");
                return;
            }

            // Procura a primeira notícia que contenha "bilhete" ou "bilhetes"
            var noticiaBilhetes = links.FirstOrDefault(a => 
                !string.IsNullOrWhiteSpace(a.InnerText) && 
                a.InnerText.Contains("bilhete", StringComparison.OrdinalIgnoreCase));

            if (noticiaBilhetes != null)
            {
                // Limpa o texto (tira quebras de linha e espaços extra que o HTML costuma ter)
                string titulo = noticiaBilhetes.InnerText.Trim();
                titulo = Regex.Replace(titulo, @"\s+", " ");
                string safeTitle = titulo.Replace("\"", "\\\""); // Evita que aspas partam o JSON do Discord
                
                string linkParcial = noticiaBilhetes.GetAttributeValue("href", "");
                // Alguns links no site podem ser relativos (ex: /pt/noticias/...), isto garante o link completo
                string linkCompleto = linkParcial.StartsWith("http") ? linkParcial : $"https://www.fcporto.pt{linkParcial}";

                // Verifica o que está gravado na memória
                string ultimoLinkAvisado = "";
                if (File.Exists(fileMemory))
                {
                    ultimoLinkAvisado = File.ReadAllText(fileMemory).Trim();
                }

                // Se for um link novo, envia para o Discord
                if (linkCompleto != ultimoLinkAvisado)
                {
                    Console.WriteLine($"Nova notícia encontrada: {titulo}");
                    
                    string jsonPayload = $@"{{
                        ""content"": ""🚨 **Nova Informação de Bilhetes!** 🚨\n\n**{safeTitle}**\n{linkCompleto}""
                    }}";

                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(webhookUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Mensagem enviada com sucesso!");
                        File.WriteAllText(fileMemory, linkCompleto);
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