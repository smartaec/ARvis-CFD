using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Hosting.Self;

namespace CfdHost
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length>0) {
                var contentPath = args[0];
                if(Directory.Exists(contentPath)) {
                    GlobalEnv.ContentFolder=contentPath;
                }
            }
            GlobalEnv.ContentFolder=Path.GetFullPath(GlobalEnv.ContentFolder).TrimEnd(Path.DirectorySeparatorChar);

            var cfg = new HostConfiguration() {
                RewriteLocalhost=true,
                UrlReservations=new UrlReservations() { CreateAutomatically=true }
            };
            using(var host = new NancyHost(cfg, new Uri("http://localhost:1234")//, new Uri("http://localhost:80")
                )) {
                host.Start();
                Console.WriteLine("CFD host server started!");
                Console.WriteLine("Contents are located in "+GlobalEnv.ContentFolder+".");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
