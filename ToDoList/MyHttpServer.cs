using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Web;
using RazorEngine;
using RazorEngine.Templating;
using Encoding = System.Text.Encoding;

namespace ToDoList
{
    public class MyHttpServer
    {
        private Thread _serverThread;
        private string _siteDirectory;
        private HttpListener _listener;
        private int _port;
        private List<Model> _toDoList;
        private Dictionary<string, string> _postParams;
        private Model _toDo;
        private const string TasksPath = @"..\..\..\data\tasks.json";
        private int _takenId;
        private Dictionary<string, string> _contentTypes = new Dictionary<string, string>
        {
            {".css",  "text/css"},
            {".html", "text/html"},
            {".ico",  "image/x-icon"},
            {".js",   "application/x-javascript"},
            {".json", "application/json"},
            {".png",  "image/png"}
        };
        
        public MyHttpServer(string path, int port)
        {
            _toDoList = new List<Model>();
            this.Initialize(path, port);
        }
        
        private void Initialize(string path, int port)
        {
            _siteDirectory = path;
            _port = port;
            _serverThread = new Thread(Listen);
            _serverThread.Start();
        }
        
        public void Stop()
        {
            _serverThread.Interrupt();
            _listener.Stop();
        }
        
        private void Listen()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://localhost:" + _port.ToString() + "/");
            _listener.Start();
            while (true)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    Process(context);
                }   
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
        
        private void Process(HttpListenerContext context)
        {
            NameValueCollection nameValueCollection = context.Request.QueryString;
            if (nameValueCollection.HasKeys())
            {
                try
                {
                    _takenId = Int32.Parse(nameValueCollection["id"]);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            string fileName = context.Request.Url.AbsolutePath;
            fileName = Path.Combine(_siteDirectory, fileName[1..]);
            if (!File.Exists(TasksPath))
            {
                File.Create(TasksPath).Close();
            }
            string readJson = File.ReadAllText(TasksPath);
            if (context.Request.HttpMethod.Equals("POST"))
            {
                System.IO.Stream body = context.Request.InputStream;
                System.Text.Encoding encoding = context.Request.ContentEncoding;
                System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);   //чтение формы
                string data = reader.ReadToEnd();
                _postParams = new Dictionary<string, string>();
                string[] rawParams = data.Split('&');                  //Разделение на ключь значение 
                foreach (string param in rawParams)
                {
                    string[] kvPair = param.Split('=');
                    string key = kvPair[0];
                    string value = HttpUtility.UrlDecode(kvPair[1]);
                    _postParams.Add(key, value);
                }
                if (_postParams.ContainsValue("delete"))
                {
                    for (int i = 0; i < _toDoList.Count; i++)
                    {
                        if (_toDoList[i].Id == _takenId)
                        {
                            _toDoList.Remove(_toDoList[i]);
                            SaveDataToJson(_toDoList);
                        }
                    }
                }
                else if (_postParams.ContainsValue("execute"))
                {
                    for (int i = 0; i < _toDoList.Count; i++)
                    {
                        if (_toDoList[i].Id == _takenId)
                        {
                            _toDoList[i].Status = "done";
                            _toDoList[i].EndDate = DateTime.Now.ToString("dd/MM/yyyy");
                            SaveDataToJson(_toDoList);
                        }
                    }
                }
                else if(_postParams.ContainsKey("name") && _postParams.ContainsKey("creator") && _postParams.ContainsKey("description"))
                {
                    if (readJson == "" || readJson == "[]")                     //Проверка файла 
                    {
                        _toDoList.Add(CreateTask());
                        SaveDataToJson(_toDoList);
                    }
                    else
                    {
                        _toDoList = JsonSerializer.Deserialize<List<Model>>(readJson);
                        _toDoList.Add(CreateTask());
                        SaveDataToJson(_toDoList);
                    }
                }
            }
            
            else
            {
                if (!(readJson is "" or "[]"))
                    _toDoList = JsonSerializer.Deserialize<List<Model>>(readJson);
            }
            string content = fileName.Contains(".html")    //билд страницы
                ? BuildHtml(fileName)
                : File.ReadAllText(fileName);

            if (File.Exists(fileName))
            {
                try
                {
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(content);
                    Stream fileStream = new MemoryStream(htmlBytes);

                    context.Response.ContentType = GetContentType(fileName);
                    context.Response.ContentLength64 = fileStream.Length;
                    byte[] buffer = new byte[16 * 1024];
                    int dataLength;
                    do
                    {
                        dataLength = fileStream.Read(buffer, 0, buffer.Length);
                        context.Response.OutputStream.Write(buffer, 0, dataLength);
                    } while (dataLength > 0);
                    fileStream.Close();
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Flush();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            context.Response.OutputStream.Close();
        }

        private string GetContentType(string filename)
        {
            string fileExtension = Path.GetExtension(filename);
            _contentTypes.TryGetValue(fileExtension, out string contentType);
            return contentType;
        }
        
        private string BuildHtml(string filePath)
        {
            string layoutPath = Path.Combine(_siteDirectory, "layout.html");
            var razorService = Engine.Razor;
            if(!razorService.IsTemplateCached("layout", typeof(Model)))
                razorService.AddTemplate("layout", File.ReadAllText(layoutPath));
            if (!razorService.IsTemplateCached(filePath, typeof(Model)))
            {
                razorService.AddTemplate(filePath, File.ReadAllText(filePath));
                razorService.Compile(filePath, typeof(Model));
            }

            string resultHttp = razorService.Run(filePath, typeof(Model), new Model
            {
                ListOfToDo = _toDoList,
                TakenId = _takenId
            });
            
            return resultHttp;
        }


        private Model CreateTask()
        {
            try
            {
                _toDo = new Model();
                _toDo.Name = _postParams["name"];
                _toDo.CreateDate = DateTime.Now.ToString("dd/MM/yyyy");
                _toDo.Creator = _postParams["creator"];
                _toDo.Description = _postParams["description"];
                if (!_toDoList.Any())
                    _toDo.Id = 1;
                else
                {
                    _toDo.Id = _toDoList.OrderByDescending(i => i.Id).FirstOrDefault().Id + 1;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return _toDo;
        }

        private void SaveDataToJson(List<Model> taskList)
        {
            try
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                string json = JsonSerializer.Serialize(taskList, options);
                File.WriteAllText(TasksPath, json);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}