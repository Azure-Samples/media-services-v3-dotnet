using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace StreamExistingMP4Utils
{
    public static class ManifestUtils
    {
        public static string AddIsmcToIsm(string ismXmlContent, string newIsmcFileName)
        {
            // Example head tag for the ISM on how to include the ISMC.
            // <head>
            //   <meta name = "clientManifestRelativePath" content = "GOPR0881.ismc" />
            //   <meta name = "formats" content = "mp4" />
            //   <meta name = "fragmentsPerHLSSegment" content ="1" />
            // </ head >

            string manPath = "clientManifestRelativePath";

            byte[] array = Encoding.ASCII.GetBytes(ismXmlContent);
            // Checking and removing Byte Order Mark (BOM) for UTF-8 if present.
            if (array[0] == 63)
            {
                byte[] tempArray = new byte[array.Length - 1];
                Array.Copy(array, 1, tempArray, 0, tempArray.Length);
                ismXmlContent = Encoding.UTF8.GetString(tempArray);
            }

            XDocument doc = XDocument.Parse(ismXmlContent);
            XNamespace ns = "http://www.w3.org/2001/SMIL20/Language";

            // If the node is already there we should skip this asset.  Maybe.  Or maybe update it?
            if (doc != null)// && ismXmlContent.IndexOf("clientManifestRelativePath") < 0)
            {
                XElement bodyhead = doc.Element(ns + "smil").Element(ns + "head");
                var element = new XElement(ns + "meta", new XAttribute("name", manPath), new XAttribute("content", newIsmcFileName));

                XElement manifestRelPath = bodyhead.Elements(ns + "meta").Where(e => e.Attribute("name").Value == manPath).FirstOrDefault();
                if (manifestRelPath != null)
                {
                    manifestRelPath.ReplaceWith(element);
                }
                else
                {
                    bodyhead.Add(element);
                }
            }
            else
            {
                throw new Exception("Xml document cannot be read or is empty.");
            }
            return doc.Declaration.ToString() + Environment.NewLine + doc.ToString();
        }

        public static string RemoveXmlNode(string ismcContentXml)
        {
            var doc = new XmlDocument();
            doc.LoadXml(ismcContentXml);
            XmlNode node = doc.SelectSingleNode("//SmoothStreamingMedia");
            XmlNode child = doc.SelectSingleNode("//Protection");
            node.RemoveChild(child);
            return doc.OuterXml;
        }


        public static async Task<GeneratedServerManifest> LoadAndUpdateManifestTemplateAsync(BlobContainerClient container)
        {
            // Let's list the blobs
            var allBlobs = new List<BlobItem>();
            await foreach (Azure.Page<BlobItem> page in container.GetBlobsAsync().AsPages())
            {
                allBlobs.AddRange(page.Values);
            }
            IEnumerable<BlobItem> blobs = allBlobs.Where(c => c.Properties.BlobType == BlobType.Block).Select(c => c);


            BlobItem[] mp4AssetFiles = blobs.Where(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)).ToArray();
            BlobItem[] m4aAssetFiles = blobs.Where(f => f.Name.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)).ToArray();
            BlobItem[] mediaAssetFiles = blobs.Where(f => f.Name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || f.Name.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (mediaAssetFiles.Length != 0)
            {
                // Prepare the manifest
                string mp4fileuniqueaudio = null;
                XDocument doc = XDocument.Load(Path.Combine(Environment.CurrentDirectory, @"./manifest.ism"));

                XNamespace ns = "http://www.w3.org/2001/SMIL20/Language";

                XElement bodyxml = doc.Element(ns + "smil");
                XElement body2 = bodyxml.Element(ns + "body");

                XElement switchxml = body2.Element(ns + "switch");

                // audio tracks (m4a)
                foreach (BlobItem file in m4aAssetFiles)
                {
                    switchxml.Add(new XElement(ns + "audio", new XAttribute("src", file.Name), new XAttribute("title", Path.GetFileNameWithoutExtension(file.Name))));
                }

                if (m4aAssetFiles.Length == 0)
                {
                    // audio track(s)
                    IEnumerable<BlobItem> mp4AudioAssetFilesName = mp4AssetFiles.Where(f =>
                                                               (f.Name.ToLower().Contains("audio") && !f.Name.ToLower().Contains("video"))
                                                               ||
                                                               (f.Name.ToLower().Contains("aac") && !f.Name.ToLower().Contains("h264"))
                                                               );

                    IOrderedEnumerable<BlobItem> mp4AudioAssetFilesSize = mp4AssetFiles.OrderBy(f => f.Properties.ContentLength);

                    string mp4fileaudio = (mp4AudioAssetFilesName.Count() == 1) ? mp4AudioAssetFilesName.FirstOrDefault().Name : mp4AudioAssetFilesSize.FirstOrDefault().Name; // if there is one file with audio or AAC in the name then let's use it for the audio track
                    switchxml.Add(new XElement(ns + "audio", new XAttribute("src", mp4fileaudio), new XAttribute("title", "audioname")));

                    if (mp4AudioAssetFilesName.Count() == 1 && mediaAssetFiles.Length > 1) //looks like there is one audio file and one other video files
                    {
                        mp4fileuniqueaudio = mp4fileaudio;
                    }
                }

                // video tracks
                foreach (BlobItem file in mp4AssetFiles)
                {
                    if (file.Name != mp4fileuniqueaudio) // we don't put the unique audio file as a video track
                    {
                        switchxml.Add(new XElement(ns + "video", new XAttribute("src", file.Name)));
                    }
                }

                // manifest filename
                string name = CommonPrefix(mediaAssetFiles.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToArray());
                if (string.IsNullOrEmpty(name))
                {
                    name = "manifest";
                }
                else if (name.EndsWith("_") && name.Length > 1) // i string ends with "_", let's remove it
                {
                    name = name.Substring(0, name.Length - 1);
                }
                name += ".ism";

                return new GeneratedServerManifest() { Content = doc.Declaration.ToString() + Environment.NewLine + doc.ToString(), FileName = name };
            }
            else
            {
                return new GeneratedServerManifest() { Content = null, FileName = string.Empty }; // no mp4 in asset
            }
        }

        private static string CommonPrefix(string[] ss)
        {
            if (ss.Length == 0)
            {
                return string.Empty;
            }

            if (ss.Length == 1)
            {
                return ss[0];
            }

            int prefixLength = 0;

            foreach (char c in ss[0])
            {
                foreach (string s in ss)
                {
                    if (s.Length <= prefixLength || s[prefixLength] != c)
                    {
                        return ss[0].Substring(0, prefixLength);
                    }
                }
                prefixLength++;
            }

            return Slugify(ss[0]); // all strings identical
        }

        public static string ReturnS(int number)
        {
            return number > 1 ? "s" : string.Empty;
        }

        private static string Slugify(this string phrase)
        {
            string str = phrase.RemoveAccent().ToLower();
            str = System.Text.RegularExpressions.Regex.Replace(str, @"[^a-z0-9\s-]", ""); // Remove all non valid chars          
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s+", " ").Trim(); // convert multiple spaces into one space  
            str = System.Text.RegularExpressions.Regex.Replace(str, @"\s", "-"); // //Replace spaces by dashes
            return str;
        }

        private static string RemoveAccent(this string txt)
        {
            byte[] bytes = System.Text.Encoding.GetEncoding("Cyrillic").GetBytes(txt);
            return System.Text.Encoding.ASCII.GetString(bytes);
        }

        public static async Task<List<string>> GetManifestFilesListFromStorageAsync(BlobContainerClient storageContainer)
        {
            var fullBlobList = new List<BlobItem>();
            await foreach (Azure.Page<BlobItem> page in storageContainer.GetBlobsAsync().AsPages())
            {
                fullBlobList.AddRange(page.Values);
            }

            // Filter the list to only contain .ism and .ismc files
            IEnumerable<string> filteredList = from b in fullBlobList
                                               where b.Properties.BlobType == BlobType.Block && b.Name.ToLower().Contains(".ism")
                                               select b.Name;
            return filteredList.ToList();
        }
    }
}
