using ClinicaPsi.Application.Tests;

namespace ClinicaPsi.PdfExemplo;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("================================================");
        Console.WriteLine("  GERADOR DE PDF DE EXEMPLO - ClinicaPsi");
        Console.WriteLine("================================================");
        Console.WriteLine();
        Console.WriteLine("Gerando PDF com dados fictícios...");
        Console.WriteLine();

        try
        {
            // Gerar PDF
            var pdfBytes = PdfExemploGenerator.GerarPdfExemplo();
            
            // Salvar arquivo
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = $"ClinicaPsi_Historico_Exemplo_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(desktopPath, fileName);
            
            File.WriteAllBytes(filePath, pdfBytes);
            
            Console.WriteLine("✓ PDF gerado com sucesso!");
            Console.WriteLine();
            Console.WriteLine($"Arquivo salvo em:");
            Console.WriteLine($"{filePath}");
            Console.WriteLine();
            Console.WriteLine("Detalhes do PDF:");
            Console.WriteLine("- Paciente: Maria Silva Santos");
            Console.WriteLine("- Total de consultas: 13");
            Console.WriteLine("- Consultas realizadas: 12");
            Console.WriteLine("- Consultas canceladas: 1");
            Console.WriteLine("- Valor total: R$ 1.800,00");
            Console.WriteLine("- PsicoPontos: 7 pontos");
            Console.WriteLine();
            Console.WriteLine("Abra o arquivo para visualizar o relatório completo!");
            Console.WriteLine();
            
            // Tentar abrir o PDF automaticamente
            Console.Write("Deseja abrir o PDF agora? (S/N): ");
            var resposta = Console.ReadLine()?.ToUpper();
            
            if (resposta == "S")
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
                Console.WriteLine("Abrindo PDF...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Erro ao gerar PDF: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine($"Detalhes: {ex}");
        }
        
        Console.WriteLine();
        Console.WriteLine("Pressione qualquer tecla para sair...");
        Console.ReadKey();
    }
}
