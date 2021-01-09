
        public JObject downloadExtension(string url)
        {
            Regex extensionPattern = new Regex(@"https:\/\/chrome\.google\.com\/webstore\/detail\/(.+)\/([^?]+)");

            dynamic result = new JObject();

            result.success = false;
            result.reason = "";

            try
            {
                var groups = extensionPattern.Match(url).Groups;

                if (groups.Count != 3)
                    throw new Exception("Invalid chrome store url");

                string extensionId = groups[2].Value;
                string extensionName = groups[1].Value;


                extensionName = Regex.Replace(extensionName, "%[A-Za-z0-9]{2}", "");
                extensionName = Regex.Replace(extensionName, "\\-\\-", "-").Trim(new char[] { '-' });

                if (extensionName.Length == 0)
                    extensionName = extensionId;

                byte[] buffer = null;

                try
                {
                    using (System.Net.WebClient wc = new System.Net.WebClient())
                        buffer = wc.DownloadData("https://clients2.google.com/service/update2/crx?response=redirect&prodversion=49.0&acceptformat=crx3&x=id%3D" + extensionId + "%26installsource%3Dondemand%26uc");
                }
                catch (System.Net.WebException x)
                {
                    if (((System.Net.HttpWebResponse)x.Response).StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new Exception("That extension does not exist in the chrome store");
                    }
                }

                if (buffer == null || buffer.Length == 0)
                    throw new Exception("Failed to download the file");


                var zipBytes = unpackCrx(buffer);

                string tempZip = Path.GetFullPath("./temp.zip");

                File.WriteAllBytes(tempZip, zipBytes);

                string finalPath = Path.GetFullPath(EXTENSIONS_FOLDER + "\\" + extensionName);

                Invoke(new Action(delegate ()
                {
                    WFunctions.UnZip(tempZip, finalPath);
                }));

                result.success = true;
            }
            catch (Exception x)
            {
                result.reason = "Error: " + x.Message;
            }

            return result;
        }

        public byte[] unpackCrx(byte[] buffer)
        {
            const int V2_HEADER_SIZE = 16,
                      V3_HEADER_SIZE = 12;

            byte[] result = null;

            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (BinaryReader br = new BinaryReader(ms))
                {
                    if (br.ReadInt32() != 875721283)
                        throw new Exception("Missing cr24 header");

                    uint offset = 0;
                    uint version = br.ReadUInt32();

                    if (version == 2)
                    {
                        uint publicKeyLength = br.ReadUInt32();
                        uint signatureLength = br.ReadUInt32();

                        offset = V2_HEADER_SIZE + publicKeyLength + signatureLength;
                    }
                    else if (version == 3)
                    {
                        uint headerSize = br.ReadUInt32();

                        offset = V3_HEADER_SIZE + headerSize;
                    }
                    else
                    {
                        throw new Exception("Unsupported CRX version");
                    }

                    result = buffer.Skip((int)offset).ToArray();
                }
            }

            return result;
        }
