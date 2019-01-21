using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Nancy.Responses;

namespace CfdHost
{
    public class HomeModule :NancyModule
    {
        public HomeModule()
        {
            this.Before.AddItemToEndOfPipeline(async (ctx, token) => {
                await Task.Run(() => {
                    Console.WriteLine(string.Format("{0}: request from {1} for {2}.", DateTime.UtcNow.ToLongTimeString(), ctx.Request.UserHostAddress, ctx.Request.Url));
                });
                return ctx.Response;
            });
            //            this.After.AddItemToEndOfPipeline(async (ctx, token) => {
            //                await Task.Run(() => {
            //                    Console.WriteLine(string.Format("{0}: response succeed to {1} for {2}.", DateTime.UtcNow.ToLongTimeString(), ctx.Request.UserHostAddress, ctx.Request.Url));
            //                });
            //            });

            Get["/"]=parameters => "Hello, this is the main page for CfdHost.\n"
            +"";

            Get["/scan/{name}"]=parameters => {
                string name = (string)parameters.name;
                return CreateFileResponse("scan", name);
            };

            Get["/tubes/{name}"]=parameters => {
                string name = (string)parameters.name;
                return CreateFileResponse("tubes", name);
            };


            Get["/slices/(?<direction>[xyzXYZ])(?<position>-?[1-9]?0$)"]=parameters => {
                string dir = (string)parameters.direction;
                int pos = (int)parameters.position;
                return CreateFileResponse("slices", dir+pos);

            };

            Get["/scan"]=parameters => {
                return CreateFileListResponse("scan");
            };

            Get["/slices"]=parameters => {
                return CreateFileListResponse("slices");
            };

            Get["/tubes"]=parameters => {
                return CreateFileListResponse("tubes");
            };

        }

        Response CreateFileResponse(string dir, string name)
        {
            if(!Directory.Exists(GlobalEnv.ContentFolder+"/"+dir)) {
                return HttpStatusCode.NotFound;
            }
            var fullPath = GlobalEnv.ContentFolder+"/"+dir+"/"+name+".c4a";
            if(!File.Exists(fullPath)) {
                return HttpStatusCode.NotFound;
            }

            var queryParas = (IDictionary<string, dynamic>)this.Request.Query;
            bool compress = false;
            if(queryParas.Count>0&&queryParas.ContainsKey("compress")) {
                compress=queryParas["compress"].ToString()=="true";
            }
            var cpath = Path.GetDirectoryName(fullPath)+Path.DirectorySeparatorChar+Path.GetFileNameWithoutExtension(fullPath)+".zipc4a";
            if(compress&&!File.Exists(cpath)) {
                CreateCompressedVersion(fullPath, cpath);
            }

            var stream = new FileStream(compress ? cpath : fullPath, FileMode.Open);
            var response = Response.FromStream(stream, "application/octet-stream");//content type of meta is not accuracy
            response.Headers.Add("Content-Disposition", "attachment;filename=\""+name+(compress ? ".zipc4a\"" : ".c4a\""));
            return response;
        }

        Response CreateFileListResponse(string dir)
        {
            if(!Directory.Exists(GlobalEnv.ContentFolder+"/"+dir)) {
                return HttpStatusCode.NotFound;
            }
            var files = Directory.GetFiles(GlobalEnv.ContentFolder+"/"+dir, "*.c4a");
            if(files!=null&&files.Length>0) {
                var names = files.Select(f => Path.GetFileNameWithoutExtension(f));
                return string.Join(";", names);
            }
            return HttpStatusCode.NotFound;
        }

        static void CreateCompressedVersion(string fullpath, string cpath)
        {
            using(var gzip = new GZipStream(new FileStream(cpath, FileMode.Create), CompressionMode.Compress)) {
                using(var input = new FileStream(fullpath, FileMode.Open)) {
                    byte[] buffer = new byte[4096];
                    while(true) {
                        var c = input.Read(buffer, 0, buffer.Length);
                        if(c==0) {
                            break;
                        }
                        gzip.Write(buffer, 0, c);
                    }
                }
            }
        }
    }
}
