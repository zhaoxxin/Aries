using Aries.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.ComponentModel;

namespace Aries.Lib
{
    public static class ServerConfigExtention
    {
        public static string ToJsonString(this ServerConfig sc)
        {
            return JsonHelper.SerializeObject(sc);
        }
        public static void UpdateData(this ServerConfig sc, ServerConfig other)
        {
            foreach (var p in sc.GetType().GetProperties())
            {
                p.SetValue(sc, p.GetValue(other));
            }
        }
    }
    public static class ServerConfigService
    {

        public static WarpMessage WarpMessage;
        public static readonly string ARIESDIR = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\Aries\";
        public static readonly string FILE = ARIESDIR + @"Aries.json";
        private static UserConfig userConfig = new UserConfig();
        static string LoadFile()
        {
            try
            {
                using (var reader = new StreamReader(FILE))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {

                return LoadDefault();
            }

        }

        static string LoadDefault()
        {

            return JsonHelper.SerializeObject(new
            {
                configs = new ServerConfig[] {
                new ServerConfig {
                    ID=1,
                    ServerName="单机登录",
                    Host="127.0.0.1"
                }
            },
                lastId = 0,
                mode = 0,
                quickPass = true,
                user = new UserConfig()
            });
        }

        private static BindingList<ServerConfig> serverConfigs;
        public static int LastId { get; set; }
        public static NetForwardMode Mode { get; set; }
        public static bool QuickPass { get; set; }

        public static BindingList<ServerConfig> LoadAll()
        {
            if (serverConfigs == null)
            {
                var config = new { mode = 0,lastId = 0, quickPass = true, configs = new ServerConfig[0], user = new UserConfig() };
                config = JsonHelper.DeserializeAnonymousType(LoadFile(), config);
                LastId = config.lastId;
                Mode = (NetForwardMode)config.mode;
                QuickPass = config.quickPass;
                userConfig = NormalizeUser(config.user);

                var q = from c in config.configs
                        select c;
                serverConfigs = new BindingList<ServerConfig>(q.ToList());
            }

            return serverConfigs;
        }

        public static UserConfig LoadUser()
        {
            EnsureLoaded();
            return new UserConfig
            {
                Username = userConfig.Username,
                Password = userConfig.Password
            };
        }

        public static void SaveUser(string username, string password)
        {
            EnsureLoaded();
            userConfig = new UserConfig
            {
                Username = username ?? string.Empty,
                Password = password ?? string.Empty
            };
            SaveAll();
        }

        public static void SaveAll()
        {
            EnsureLoaded();
            if (!Directory.Exists(ARIESDIR))
            {
                Directory.CreateDirectory(ARIESDIR);
            }

            using (var writer = new StreamWriter(FILE))
            {
                writer.Write(JsonHelper.SerializeObject(new { configs = serverConfigs, lastId = LastId,mode = Mode, quickPass=QuickPass, user = userConfig }));
            }

        }

        public static void SaveOrUpdateInMemory(ServerConfig serverConfig)
        {
            EnsureLoaded();
            if (serverConfig.ID == 0)
            {
                var q = from sc in serverConfigs
                        select sc.ID;
                serverConfig.ID = q.Any() ? q.Max() + 1 : 1;
                serverConfigs.Add(serverConfig);
            }
            else
            {
                var q = from sc in serverConfigs
                        where sc.ID == serverConfig.ID
                        select sc;
                q.First().UpdateData(serverConfig);
            }
        }

        public static bool RemoveFromMemory(ServerConfig serverConfig)
        {
            EnsureLoaded();
            return serverConfigs.Remove(serverConfig);
        }

        private static void EnsureLoaded()
        {
            if (serverConfigs == null)
            {
                LoadAll();
            }
        }

        private static UserConfig NormalizeUser(UserConfig user)
        {
            if (user == null)
            {
                return new UserConfig();
            }

            return new UserConfig
            {
                Username = user.Username ?? string.Empty,
                Password = user.Password ?? string.Empty
            };
        }

    }
}
