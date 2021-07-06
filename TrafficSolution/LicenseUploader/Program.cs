using System;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace UploadData
{
    class Program
    {

        static void Main(string[] args)
        {
            //Get credentials to use to authenticate ourselves to AWS
            AWSCredentials credentials = GetAWSCredentialsByName("default");
            AmazonS3Client s3Client = new AmazonS3Client(credentials, RegionEndpoint.USEast1);


            string bucketName = "patient-2021";
            //Read the bucket name in the command line
            Console.Write("Enter file path: ");
            string filePath = Console.ReadLine();
            Console.Write("Enter location tag: ");
            string locationTag = Console.ReadLine();
            Console.Write("Enter date and time: ");
            string dateTag = Console.ReadLine();
            Console.Write("Enter violation type: ");
            string violationTag = Console.ReadLine();

            try
            {
                Tag location = new Tag
                {
                    Key = "Location",
                    Value = locationTag
                };
                Tag dateTime = new Tag
                {
                    Key = "DateTime",
                    Value = dateTag
                };
                Tag violation = new Tag
                {
                    Key = "Type",
                    Value = violationTag
                };

                List<Tag> list = new List<Tag>();
                list.Add(location);
                list.Add(dateTime);
                list.Add(violation);
                string[] split = filePath.Split("\\");

                //Put object into bucket 
                PutObjectRequest request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = split[split.Length - 1],
                    FilePath = filePath,
                    TagSet = list
                };
                PutObjectResponse response = s3Client.PutObjectAsync(request).Result;
            }
            catch (Exception e)
            {
                Console.WriteLine("{0}", e.Message);
            }
            s3Client.Dispose();
        }

        private static AWSCredentials GetAWSCredentialsByName(string profileName)
        {
            if (String.IsNullOrEmpty(profileName))
            {
                throw new ArgumentNullException("profileName cannot be null or empty");
            }

            SharedCredentialsFile credFile = new SharedCredentialsFile();
            CredentialProfile profile = credFile.ListProfiles().Find(p => p.Name.Equals(profileName));
            if (profile == null)
            {
                throw new Exception(String.Format("Profile named {0} not found", profileName));
            }

            return AWSCredentialsFactory.GetAWSCredentials(profile, new SharedCredentialsFile());
        }
    }
}
