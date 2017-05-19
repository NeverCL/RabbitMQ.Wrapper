using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ.Wrapper
{
    /// <summary>
    /// RabbitMQ 
    /// </summary>
    public class RabbitManager : IDisposable
    {
        const string TypeName = "RabbitHelper";

        public IConnection Connection { get; }

        public RabbitManager() : this((RabbitConfig)ConfigurationManager.GetSection("RabbitConfig"))
        {
        }

        public RabbitManager(string hostName, string userName, string password, string virtualHost = "/", int port = 5672)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = hostName,
                    UserName = userName,
                    Password = password,
                    VirtualHost = virtualHost, // 虚拟Host,需提前配置
                    Port = port, // Broker端口
                    AutomaticRecoveryEnabled = true,    // 断线重连01
                };
                Connection = factory.CreateConnection(); // 创建与RabbitMQ服务器的连接
                LogManager.GetLogger(TypeName).Info("初始化 RabbitMQ 完成");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(TypeName).Error(ex);
                throw;
            }
        }

        public RabbitManager(RabbitConfig config) : this(config.HostName, config.UserName, config.Password, config.VirtualHost, config.Port)
        {

        }

        public void CreateQueue(string queueName, bool durable = true)
        {
            try
            {
                using (var channel = Connection.CreateModel())  // 创建1个Channel(大部分API在该Channel中)
                {
                    // 定义1个队列,自动会和默认的exchange 做direct类型绑定
                    channel.QueueDeclare(
                        queue: queueName,                     // 队列名称
                        durable: durable,                      // 队列是否持久化
                        exclusive: false,                   // 排他队列:如果一个队列被声明为排他队列，该队列仅对首次声明它的连接可见，并在连接断开时自动删除。(活动在一次连接内)
                        autoDelete: false,                  // 自动删除:当最后一个消费者取消订阅时，队列自动删除。如果您需要仅由一个使用者使用的临时队列，请将自动删除与排除。当消费者断​​开连接时，队列将被删除。(至少消费者能连一次)
                        arguments: null);
                    LogManager.GetLogger(TypeName).Info("创建队列: " + queueName + " 成功");
                }
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(TypeName).Error(ex);
                throw;
            }
        }

        public void Send(string routingKey, object message, bool persistent = true)
        {
            Send(routingKey, JsonConvert.SerializeObject(message), persistent);
        }

        public void Send(string routingKey, string message, bool persistent = true)
        {
            try
            {
                using (var channel = Connection.CreateModel())  // 创建1个Channel(大部分API在该Channel中)
                {
                    Send4Loop(channel, routingKey, message, persistent);
                }
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(TypeName).Error(ex);
                // 断线重连02
                Thread.Sleep(1000);
                LogManager.GetLogger(TypeName).Info($"正在重发消息:{routingKey}发送:{message}...");
                Send(routingKey, message, persistent);
            }
        }

        /// <summary>
        /// 可用于循环时的发送消息
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="routingKey"></param>
        /// <param name="message"></param>
        /// <param name="persistent"></param>
        public void Send4Loop(IModel channel, string routingKey, string message, bool persistent)
        {
            var properties = channel.CreateBasicProperties();
            properties.Persistent = persistent;
            var body = Encoding.UTF8.GetBytes(message);
            channel.BasicPublish(
                exchange: string.Empty,         // 传递为Empty的时候,通过	`(AMQP default)`传递
                routingKey: routingKey,         // routing key 与 queuebind中的binding key对应
                basicProperties: properties,    // 消息header
                body: body);                    // 消息body:发送的是bytes 可以任意编码
            LogManager.GetLogger(TypeName).Info($"发送消息成功：{routingKey}-{message}");
        }

        public IModel Receive(string queueName, Action<string> action)
        {
            try
            {
                var channel = Connection.CreateModel();
                var consumer = new EventingBasicConsumer(channel);  // 创建Consumer
                consumer.Received += (model, ea) =>         // 通过回调函数异步推送我们的消息
                {
                    var body = ea.Body;
                    var message = Encoding.UTF8.GetString(body);
                    LogManager.GetLogger(TypeName).Info($"接收消息成功：{message}");
                    try
                    {
                        action(message);
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false); // 消息响应
                        LogManager.GetLogger(TypeName).Info($"成功处理消息：{message}");
                    }
                    catch (Exception ex)
                    {
                        LogManager.GetLogger(TypeName).Error(ex);
                    }
                };
                channel.BasicQos(0, 1, false);  // 设置perfetchCount=1 。这样就告诉RabbitMQ 不要在同一时间给一个工作者发送多于1个的消息
                channel.BasicConsume(queue: queueName,
                                     noAck: false,      // 需要消息响应（Acknowledgments）机制
                                     consumer: consumer);
                return channel;
            }
            catch (Exception ex)
            {
                LogManager.GetLogger(TypeName).Error(ex);
                // 断线重连03
                Thread.Sleep(1000);
                LogManager.GetLogger(TypeName).Info($"Receive 断线重连:{queueName}...");
                Receive(queueName, action);
            }
            return null;
        }

        public void Dispose()
        {
            Connection.Dispose();
        }
    }
}
