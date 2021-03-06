﻿using System;
using System.Collections.Generic;
using System.IO;
using Duplicati.Library.Interface;

namespace Duplicati.Library.Backend
{
    public class Dropbox : IBackend, IStreamingBackend
    {
        private const string AUTHID_OPTION = "authid";
        private const int MAX_FILE_LIST = 10000;

        private string m_path,m_accesToken;
        private DropboxHelper dbx;

        public Dropbox()
        {
        }

        public Dropbox(string url, Dictionary<string, string> options)
        {
            var uri = new Utility.Uri(url);

            m_path = Library.Utility.Uri.UrlDecode(uri.HostAndPath);
            if (m_path.Length != 0 && !m_path.StartsWith("/"))
                m_path = "/" + m_path;

            if (m_path.EndsWith("/"))
                m_path = m_path.Substring(0, m_path.Length - 1);

            if (options.ContainsKey(AUTHID_OPTION))
                m_accesToken = options[AUTHID_OPTION];

            dbx = new DropboxHelper(m_accesToken);
        }

        public void Dispose()
        {
            // do nothing
        }

        public string DisplayName
        {
            get { return "Dropbox"; }
        }

        public string ProtocolKey
        {
            get { return "dropbox"; }
        }

		private FileEntry ParseEntry(MetaData md)
		{
			var ife = new FileEntry(md.name);
			if (md.IsFile)
			{
				ife.IsFolder = false;
				ife.Size = (long)md.size;
			}
			else
			{
				ife.IsFolder = true;
			}

			try { ife.LastModification = ife.LastAccess = DateTime.Parse(md.server_modified).ToUniversalTime(); }
			catch { }

			return ife;
		}

        public List<IFileEntry> List()
        {
            try
            {
				var list = new List<IFileEntry>();
				var lfr = dbx.ListFiles(m_path);
              
                foreach (var md in lfr.entries)
					list.Add(ParseEntry(md));

                while (lfr.has_more)
                {
                    lfr = dbx.ListFilesContinue(lfr.cursor);
                    foreach (var md in lfr.entries)
						list.Add(ParseEntry(md));
				}

                return list;
            }
            catch (DropboxException de)
            {
                if (de.errorJSON["error"][".tag"].ToString() == "path" && de.errorJSON["error"]["path"][".tag"].ToString() == "not_found")
                    throw new FolderMissingException();
				
                throw;
            }
        }

        public void Put(string remotename, string filename)
        {
            using(FileStream fs = System.IO.File.OpenRead(filename))
                Put(remotename,fs);
        }

        public void Get(string remotename, string filename)
        {
            using(FileStream fs = System.IO.File.Create(filename))
                Get(remotename, fs);
        }

        public void Delete(string remotename)
        {
            try
            {
                string path = String.Format("{0}/{1}", m_path, remotename);
                dbx.Delete(path);
            }
            catch (DropboxException de)
            {
                // we can catch some events here and convert them to Duplicati exceptions
                throw;
            }
        }

        public IList<ICommandLineArgument> SupportedCommands { get; private set; }
        
        public string Description { get; private set; }

        public void Test()
        {
            List();
        }

        public void CreateFolder()
        {
            try
            {
                dbx.CreateFolder(m_path);
            }
            catch (DropboxException de)
            {

                if (de.errorJSON["error"][".tag"].ToString() == "path" && de.errorJSON["error"]["path"][".tag"].ToString() == "conflict")
                    throw new FolderAreadyExistedException();
                throw;
            }
        }

        public void Put(string remotename, Stream stream)
        {
            try
            {
                string path = string.Format("{0}/{1}", m_path, remotename);
                dbx.UploadFile(path, stream);
            }
            catch (DropboxException de)
            {
                // we can catch some events here and convert them to Duplicati exceptions
                throw;
            }
        }

        public void Get(string remotename, Stream stream)
        {
            try
            {
                string path = string.Format("{0}/{1}", m_path, remotename);
                dbx.DownloadFile(path, stream);
            }
            catch (DropboxException de)
            {
                // we can catch some events here and convert them to Duplicati exceptions
                throw;
            }
        }
    }
}