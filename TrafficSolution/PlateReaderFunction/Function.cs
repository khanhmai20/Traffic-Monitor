using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SQS;
using Amazon.SQS.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace PatientReaderFunction
{
    public class Function
    {
        IAmazonS3 S3Client { get; set; }

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {
            S3Client = new AmazonS3Client();
        }

        /// <summary>
        /// Constructs an instance with a preconfigured S3 client. This can be used for testing the outside of the Lambda environment.
        /// </summary>
        /// <param name="s3Client"></param>
        public Function(IAmazonS3 s3Client)
        {
            this.S3Client = s3Client;
        }

        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
        /// to respond to S3 notifications.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var s3Event = evnt.Records?[0].S3;
            if (s3Event == null)
            {
                return null;
            }

            try
            {
                var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
                string bucketName = s3Event.Bucket.Name;
                string objectKey = s3Event.Object.Key;

                GetObjectTaggingRequest tagRequest = new GetObjectTaggingRequest();
                tagRequest.Key = objectKey;
                tagRequest.BucketName = bucketName;
                Task<GetObjectTaggingResponse> tagResponse = S3Client.GetObjectTaggingAsync(tagRequest);

                List<Tag> tagset = tagResponse.Result.Tagging;

                foreach (Tag tag in tagResponse.Result.Tagging)
                {
                    Console.WriteLine("{0}:{1}", tag.Key, tag.Value);
                }

                /*string id = "";
                string name = "";

                Stream stream = await S3Client.GetObjectStreamAsync(bucketName, objectKey, null);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string content = reader.ReadToEnd();
                    
                    List<String> list = xmlProcessor(content);
                    id = list[0];
                    name = list[1];
                  
                    Console.WriteLine("Id: {0}. Name: {1}", id, name);
                }*/
                Stream stream = await S3Client.GetObjectStreamAsync(bucketName, objectKey, null);
                Image image = new Image();
                MemoryStream ms = new MemoryStream();
                stream.CopyTo(ms);
                image.Bytes = ms;

                var regClient = new AmazonRekognitionClient();
                var textRequest = new DetectTextRequest
                {
                    Image = image
                };

                var textResponse = regClient.DetectTextAsync(textRequest);
                bool cali = false;
                string plate = "";
                foreach (TextDetection text in textResponse.Result.TextDetections)
                {
                    if (IsCapitalLettersAndNumbers(text.DetectedText))
                    {
                        plate = text.DetectedText;
                    }
                    if (text.DetectedText.Contains("california", StringComparison.OrdinalIgnoreCase))
                    {
                        cali = true;
                    }
                }

                if (!cali)
                {
                    /*string outOfState = "out-of-state";
                    PutObjectRequest requestS3 = new PutObjectRequest
                    {
                        BucketName = "out-of-state",
                        Key = objectKey,
                        InputStream = stream,
                        TagSet = tagset
                    };
                    PutObjectResponse responseS3 = S3Client.PutObjectAsync(requestS3).Result;*/
                    Console.WriteLine("Transfer bucket");
                    return response.Headers.ContentType; ; 
                }
                     
                Console.WriteLine("This vehicle comes from california with license: {0}", plate);
                string message = "{ \"license\" : \"" + plate + "\", \"location\" : \"" + tagset[2].Value + "\", \"date\" : \"" + tagset[1].Value + "\", \"type\" : \"" + tagset[0].Value + "\"}";
                Console.WriteLine(message);

                AmazonSQSClient client = new AmazonSQSClient();

                //string message = "{ \"id\" : \"" + id + "\", \"name\":\"" + name + "\"}";

                var urlRequest = new GetQueueUrlRequest
                {
                    QueueName = "DownwardQueue"
                };
                var urlResponse = client.GetQueueUrlAsync(urlRequest);
                string inputQueue = urlResponse.Result.QueueUrl;

                SendMessageRequest request = new SendMessageRequest
                {
                    QueueUrl = inputQueue,
                    MessageBody = message
                };

                Task<SendMessageResponse> qresponse = client.SendMessageAsync(inputQueue,message);
                Console.WriteLine(qresponse.Result.MD5OfMessageBody);
                Console.WriteLine(qresponse.Result.MessageId);
                Console.WriteLine("Message successfully sent to queue {0}", inputQueue);

                return response.Headers.ContentType;
            }
            catch (Exception e)
            {
                context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
                context.Logger.LogLine(e.Message);
                context.Logger.LogLine(e.StackTrace);
                throw;
            }

        }

        /*private List<String> xmlProcessor(string content)
        {
            List<String> list = new List<string>();

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(content);

            XmlElement root = xmlDoc.DocumentElement;

            string id = root.SelectSingleNode("id").InnerText;
            string name = root.SelectSingleNode("name").InnerText;

            Console.WriteLine("{0} {1}", id, name);

            list.Add(id);
            list.Add(name);

            return list;
        }*/

        private static bool IsCapitalLettersAndNumbers(string s)
        {
            if (String.IsNullOrEmpty(s))
            {
                return false;
            }

            bool allDigits = true;
            foreach (char c in s)
            {
                if (!char.IsDigit(c))
                {
                    allDigits = false;
                    break;
                }
            }

            if (allDigits) return false;
            if (s.Length < 6 || s.Length > 7) return false;
            return System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Z0-9]*$");
        }
    }
}
