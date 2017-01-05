using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace ImageAzureUploader
{
    class Program
    {
        private static Assembly _assembly;
        private static Stream _imageStream;

        /// <summary>
        /// Three ways to upload files to blob storage in Azure:
        /// Upload a stream, a byte array if they are less than 64MB 
        /// or for large files break into chunks to upload
        /// 
        /// Create an Azure Storage Account and put the connection
        /// info in the app settings
        /// 
        /// This first deletes all the files in the storage container named 'testimages', 
        /// so don't use an existing container
        ///
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {

            _assembly = Assembly.GetExecutingAssembly();
            string[] resources = _assembly.GetManifestResourceNames();

            // connect to storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // connect to container
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("testimages");
            container.CreateIfNotExists(); // create container

            // delete all images in container
            foreach (IListBlobItem item in container.ListBlobs(null, true))
            {
                CloudBlockBlob listBlob = (CloudBlockBlob) item;
                listBlob.Delete();
            }

            // upload using streams (files < 64MB in size)
            foreach (var resource in resources.Where(x => x.EndsWith("jpg")))
            {
                // get image stream
                var filename = resource.Substring(resource.IndexOf("images.") + 7);
                _imageStream = _assembly.GetManifestResourceStream(resource);

                // byte array containing image
                byte[] imgBytes = Helper.GetStreamBytes(_imageStream);

                // upload blog to container
                var imageStream = new MemoryStream(imgBytes);
                var blob = container.GetBlockBlobReference("stream_" + filename);

                // upload files up to 64MB using stream upload
                blob.UploadFromStream(imageStream);
                Console.WriteLine("Upload from stream: stream_" + filename);
            }

            // upload directly from byte array
            foreach (var resource in resources.Where(x => x.EndsWith("jpg")))
            {
                // get image stream
                var filename = resource.Substring(resource.IndexOf("images.") + 7);
                _imageStream = _assembly.GetManifestResourceStream(resource);

                // byte array containing image
                byte[] imgBytes = Helper.GetStreamBytes(_imageStream);
                var blob = container.GetBlockBlobReference("array_" + filename);
                blob.UploadFromByteArray(imgBytes, 0, imgBytes.Length);
                Console.WriteLine("Upload from array: array_" + filename);
            }

            // for large files, split file into chunks to upload
            var bigBlob = container.GetBlockBlobReference("meankittysong.mp4");
            var blocklist = new List<string>();

            var largeFileName = resources.First(x => x.EndsWith("mp4"));
            byte[] fileBytes = Helper.GetStreamBytes(_assembly.GetManifestResourceStream(largeFileName));

            int id = 0;
            int byteslength = fileBytes.Length;
            int idx = 0;
            string blockId;
            int chunkSize = 250 * 1024; //250KB per block

            // upload each chunk of the file
            do
            {
                byte[] buffer = new byte[chunkSize];
				// set limit to chunksize of byteslength is larger, else use byteslength
                int limit = byteslength > chunkSize ? idx + chunkSize : idx + byteslength;
                for (int i = 0; idx < limit; idx++)
                {
                    buffer[i] = fileBytes[idx];
                    i++;
                }

                blockId = Convert.ToBase64String(BitConverter.GetBytes(id));
                bigBlob.PutBlock(blockId, new MemoryStream(buffer, true), null); // upload chunk
                blocklist.Add(blockId);
                Console.WriteLine("Upload block " + id);
                id++;
            } while (byteslength - idx > chunkSize);

            int final = byteslength - idx;
            byte[] finalbuffer = new byte[final];
            for (int loops = 0; idx < byteslength; idx++)
            {
                finalbuffer[loops] = fileBytes[idx];
                loops++;
            }
            blockId = Convert.ToBase64String(BitConverter.GetBytes(id));
            bigBlob.PutBlock(blockId, new MemoryStream(finalbuffer, true), null);
            blocklist.Add(blockId);
            Console.WriteLine("Upload block " + id);

            // finalize the uploaded file
            Console.WriteLine("finalize");
            bigBlob.PutBlockList(blocklist);
        }
    }
}
