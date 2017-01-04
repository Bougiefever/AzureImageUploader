using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageAzureUploader
{
    public static class Helper
    {
        public static byte[] GetStreamBytes(Stream stream)
        {
            var mem = new MemoryStream();
            byte[] buff = new byte[4096];
            int read;
            while ((read = stream.Read(buff, 0, buff.Length)) > 0)
            {
                mem.Write(buff, 0, read);
            }

            byte[] bytes = mem.ToArray();

            return bytes;
        }
    }
}
