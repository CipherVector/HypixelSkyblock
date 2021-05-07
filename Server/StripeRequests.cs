using System;
using System.IO;
using WebSocketSharp.Server;
using Stripe;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class StripeRequests
    {
        public async Task ProcessStripe(HttpRequestEventArgs e)
        {
            Console.WriteLine("received callback from stripe --");

            try
            {
                Console.WriteLine("reading json");
                var json = new StreamReader(e.Request.InputStream).ReadToEnd();
                //Console.WriteLine(e.)

                var stripeEvent = EventUtility.ConstructEvent(
                  json,
                  e.Request.Headers["Stripe-Signature"],
                  Program.StripeSigningSecret
                );
                Console.WriteLine("stripe valiadted");

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    Console.WriteLine("stripe checkout completed");
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;

                    // Fulfill the purchase...
                    await this.FulfillOrder(session);
                }
                else
                {
                    Console.WriteLine("sripe  is not comlete type of " + stripeEvent.Type);
                }


                e.Response.StatusCode = 200;
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"Ran into exception for stripe callback {ex.Message}");
                e.Response.StatusCode = 400;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ran into an unknown error :/ {ex.Message} {ex.StackTrace}");
            }
        }

        private async Task FulfillOrder(Stripe.Checkout.Session session)
        {
            var googleId = session.ClientReferenceId;
            var id = session.CustomerId;
            var email = session.CustomerEmail;
            var days = Int32.Parse(session.Metadata["days"]);
            Console.WriteLine("STRIPE");
            using (var context = new HypixelContext())
            {
                var user = await context.Users.Where(u => u.GoogleId == googleId).FirstAsync();
                Server.AddPremiumTime(days, user);
                user.Email = email + DateTime.Now;
                context.Update(user);
                await context.SaveChangesAsync();
                Console.WriteLine("order completed");
            }
        }
    }
}