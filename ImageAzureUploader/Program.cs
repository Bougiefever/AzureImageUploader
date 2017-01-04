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
                var filename = resource.Substring(resource.IndexOf(".image") + 1);
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
                var filename = resource.Substring(resource.IndexOf(".image") + 1);
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

            int blockId = 0;
            int byteslength = fileBytes.Length;
            int bytesread = 0;
            int index = 0;

            int numBytesPerChunk = 250 * 1024; //250KB per block

            // upload each chunk of the file
            do
            {
                byte[] buffer = new byte[numBytesPerChunk];
                int limit = index + numBytesPerChunk;
                for (int i = 0; index < limit; index++)
                {
                    buffer[i] = fileBytes[index];
                    i++;
                }
                bytesread = index;
                string blockIdBase64 = Convert.ToBase64String(System.BitConverter.GetBytes(blockId));

                bigBlob.PutBlock(blockIdBase64, new MemoryStream(buffer, true), null); // upload chunk
                blocklist.Add(blockIdBase64);
                blockId++;
            } while (byteslength - bytesread > numBytesPerChunk);

            int final = byteslength - bytesread;
            byte[] finalbuffer = new byte[final];
            for (int loops = 0; index < byteslength; index++)
            {
                finalbuffer[loops] = fileBytes[index];
                loops++;
            }
            string blobBlockId = Convert.ToBase64String(System.BitConverter.GetBytes(blockId));
            bigBlob.PutBlock(blobBlockId, new MemoryStream(finalbuffer, true), null);
            blocklist.Add(blobBlockId);

            // finalize the uploaded file
            bigBlob.PutBlockList(blocklist);
        }
    }
}
