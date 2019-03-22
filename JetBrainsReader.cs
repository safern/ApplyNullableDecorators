using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace ApplyNullableDecorators
{
    internal static class JetBrainsReader
    {
        public static IDictionary<string, XmlNode> GetAllAnnotatedApis(string[] filePaths)
        {
            Dictionary<string, XmlNode> annotatedMembers = new Dictionary<string, XmlNode>();
            foreach (string file in filePaths)
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(file);

                XmlNodeList memberNodes = doc.GetElementsByTagName("member");
                foreach (XmlNode node in memberNodes)
                {
                    annotatedMembers.TryAdd(node.Attributes["name"].Value, node);
                }
            }

            return annotatedMembers;
        }
    }
}
