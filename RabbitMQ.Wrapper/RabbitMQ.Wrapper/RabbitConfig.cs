using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RabbitMQ.Wrapper
{
    /// <summary>
    /// RabbitMQ 配置
    /// </summary>
    public sealed class RabbitConfig : ConfigurationSection
    {
        [ConfigurationProperty("HostName", DefaultValue = "localhost")]
        public string HostName { get; set; }

        [ConfigurationProperty("UserName", DefaultValue = "admin")]
        public string UserName
        {
            get { return (string)this["UserName"]; }
            set { this["UserName"] = value; }
        }

        [ConfigurationProperty("Password", DefaultValue = "admin")]
        public string Password
        {
            get { return (string)this["Password"]; }
            set { this["Password"] = value; }
        }

        [ConfigurationProperty("VirtualHost", DefaultValue = "/")]
        public string VirtualHost
        {
            get { return (string)this["VirtualHost"]; }
            set { this["VirtualHost"] = value; }
        }

        [ConfigurationProperty("Port", DefaultValue = 5672)]
        public int Port
        {
            get { return (int)this["Port"]; }
            set { this["Port"] = value; }
        }
    }

}
