using System;

namespace FoxyLink.RabbitMQ
{
    public class MessageParameters
    {
        public string Name { get; set; }
        public string Exchange { get; set; }
        public string Operation { get; set; }
        public string Type { get; set; }

        public bool BPMNEngine { get; set; } = false;

        public MessageParameters(string type)
        {
            if (type == null)
            {
                throw new ArgumentNullException("Properties.Type", "RabbitMQ message parameter {Properties.Type} not set. You must set it before using RabbitMQHost.");
            }

            var result = type.Split(".");
            if (result.Length != 1 && result.Length != 4)
            {
                throw new ArgumentOutOfRangeException("Properties.Type", $"Failed to process parameter {type}. Expected number of type parts is {{1}} for BPMN engine, {{4}} for 1C:Enterprise or Corezoid and received number is {result.Length}.");
            }

            Name = result[0];
            if (result.Length == 4)
            {
                Exchange = result[1];
                Operation = result[2];
                Type = result[3];

                if (Type.ToUpper() != "ASYNC" && Type.ToUpper() != "SYNC" && Type.Length != 40)
                {
                    throw new ArgumentException("Properties.Type", $"Failed to process parameter {type}. Expected type is {{async\\sync or Corezoid identifier}} and received type is {Type}.");
                }
            }
            else
            {
                BPMNEngine = true;
            }
        }
    }
}
