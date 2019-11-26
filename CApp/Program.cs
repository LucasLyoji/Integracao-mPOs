using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading.Tasks;

using PagarMe;
using PagarMe.Mpos;
using PaymentMethod = PagarMe.Mpos.PaymentMethod;

namespace CApp
{
    class Program
    {
        static void Main(string[] args)
        {
              Process().Wait();
        }

        public static async Task Process()
        {
            var processor = new PaymentProcessor("COM3");

            Console.WriteLine(" Bem Vindo a minha integração mPOS");
            Console.WriteLine(" Iniciando ");
            await processor.Initialize();

            System.Threading.Thread.Sleep(3000);
            Console.Write("Digite o valor da transação: ");
            Int32 integerAmount = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Digite a forma de pagamento! 1 para débito e 2 para crédito");
            Int32 paymentMethod = Int32.Parse(Console.ReadLine());
            Console.WriteLine("Por favor insira o cartão.");
            await processor.Pay(integerAmount, paymentMethod);
            
        }
    }
    public class PaymentProcessor
    {
        private readonly Mpos mpos;
        private readonly SerialPort port;

        public PaymentProcessor(string device)
        {
            port = new SerialPort(device, 140000, Parity.None, 8, StopBits.One);
            port.Open();

            mpos = new Mpos(port.BaseStream, "ek_test_SF2MPmKaWRLywNuetObE0vkv0nnkel", " ");
            mpos.NotificationReceived += (sender, e) => Console.WriteLine("Status: {0}", e);
            mpos.TableUpdated += (sender, e) => Console.WriteLine("LOADED: {0}", e);
            mpos.Errored += (sender, e) => Console.WriteLine("ERRO {0}", e);
            mpos.PaymentProcessed += (sender, e) => Console.WriteLine("CARD HASH " + e.CardHash);
            mpos.FinishedTransaction += (sender, e) => Console.WriteLine("TRANSAÇÃO CONCLUIDA!");

            PagarMeService.DefaultEncryptionKey = "ek_test_SF2MPmKaWRLywNuetObE0vkv0nnkel";
            PagarMeService.DefaultApiKey = "ak_test_jbIXhrHXHOaUUNKtUVrkT9HGL60SSg";
        }

        public async Task Initialize()
        {
            await mpos.Initialize();

            Console.WriteLine("Preparando Sincronização de tabelas.");
            await mpos.SynchronizeTables(true);
            Console.WriteLine("Tabelas Sincronizadas.");
        }

        public async Task Pay(Int32 amount, Int32 paymentMethod)
        {

            PaymentMethod pm = paymentMethod == 2 ? PaymentMethod.Credit : PaymentMethod.Debit;

            var result = await mpos.ProcessPayment(amount, null, pm);
            Console.WriteLine("Pressione Enter");
            Console.ReadLine();
            if (result.Status == PaymentStatus.Accepted)
            {
                var transaction = new Transaction
                {
                    CardHash = result.CardHash,
                    Amount = amount,
                };
                try
                {
                    await transaction.SaveAsync();
                }
                catch (PagarMe.PagarMeException ex)
                {
                    Console.WriteLine(ex.Error.Errors[0].Message);
                    await mpos.Close();
                    Console.WriteLine("FECHADO!");
                    Console.WriteLine("Fluxo de exceção acionado, transação não criada");
                    Console.ReadLine();
                }
                Console.WriteLine("TRANSACTION ID = " + transaction.Id);
                Console.WriteLine("Transaction ARC = " + transaction.AcquirerResponseCode + ", Id = " + transaction.Id);
                Console.WriteLine("ACQUIRER RESPONSE CODE = " + transaction.AcquirerResponseCode);
                int responseCode = Int32.Parse(transaction.AcquirerResponseCode);
                object emvResponse = transaction["card_emv_response"];
                var response = emvResponse?.ToString();

                if (response != null)
                {
                    await mpos.FinishTransaction(true, responseCode, response);
                }
            }
            else if (result.Status != PaymentStatus.Canceled)
            {
                await mpos.FinishTransaction(false, 0, null);
            }
            Console.WriteLine("Pressione Enter");
            Console.ReadLine();

            await mpos.Close();
            Console.WriteLine("CLOSED!");
        }
    }
}
