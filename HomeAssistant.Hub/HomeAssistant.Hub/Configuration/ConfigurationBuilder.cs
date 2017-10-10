using System;
using System.Collections.Generic;
using System.IO;

namespace HomeAssistant.Hub.Configuration
{
    public class ConfigurationBuilder : IConfigurationBuilder
    {
        private readonly IConfigurationRoot _configurationRoot;
        private string _basePath; //default?
        private IDictionary<string, bool> _configFiles = new Dictionary<string, bool>();

        public ConfigurationBuilder()
        {
            _configurationRoot = new ConfigurationRoot();
        }

        public IConfigurationBuilder AddJsonFile(string filename)
        {
            return AddJsonFile(filename, false);
        }

        public IConfigurationBuilder AddJsonFile(string filename, bool optional)
        {
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) > -1)
            {
                throw new ArgumentException("Specified filename contains invalid characters.", nameof(filename));
            }

            string baseDir = string.IsNullOrWhiteSpace(_basePath) ? Environment.CurrentDirectory : _basePath;
            filename = Path.Combine(baseDir, filename);

            if (!_configFiles.Keys.Contains(filename))
            {
                _configFiles.Add(filename, optional);
            }
            return this;
        }

        public IConfigurationRoot Build()
        {
            return _configurationRoot;
        }

        public IConfigurationBuilder SetBasePath(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Environment.CurrentDirectory, path);
            }

            if (path.IndexOfAny(Path.GetInvalidPathChars()) > -1)
            {
                throw new ArgumentException("Specified path contains invalid characters.", nameof(path));
            }

            _basePath = path;
            return this;
        }
    }
}
