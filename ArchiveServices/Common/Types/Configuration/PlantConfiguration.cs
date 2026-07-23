using System.Configuration;

namespace ArchiveServices.Common.Types.Configuration
{

    public class PlantConfiguration : ConfigurationSection
    {
        [ConfigurationProperty("Plants", IsDefaultCollection = false)]
        public PlantCollection Plants
        {
            get
            {
                PlantCollection plantsCollection = (PlantCollection)base["Plants"];
                return plantsCollection;
            }
        }

        [ConfigurationProperty("Xrefs", IsDefaultCollection = false)]
        public XrefCollection Xrefs
        {
            get
            {
                XrefCollection xrefCollection = (XrefCollection)base["Xrefs"];
                return xrefCollection;
            }
        }
    }

    public class PlantCollection : ConfigurationElementCollection
    {
        public new Plant this[string name]
        {
            get
            {
                if (IndexOf(name) < 0) return null;
                return (Plant)BaseGet(name);
            }
        }

        public Plant this[int index] => (Plant)BaseGet(index);

        public int IndexOf(string name)
        {
            name = name.ToLower();

            for (int idx = 0; idx < Count; idx++)
            {
                if (this[idx].Name.ToLower() == name)
                    return idx;
            }
            return -1;
        }

        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.BasicMap;

        protected override ConfigurationElement CreateNewElement()
        {
            return new Plant();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Plant)element).Name;
        }

        protected override string ElementName => "Plant";
    }

    public class Plant : ConfigurationElement
    {
        [ConfigurationProperty("Name", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string Name
        {
            get { return base["Name"] as string; }
            set { base["Name"] = value; }
        }
        [ConfigurationProperty("Code", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string Code
        {
            get { return base["Code"] as string; }
            set { base["Code"] = value; }
        }
        [ConfigurationProperty("Numeric", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string Numeric
        {
            get { return base["Numeric"] as string; }
            set { base["Numeric"] = value; }
        }
        [ConfigurationProperty("Offset", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string Offset
        {
            get { return base["Offset"] as string; }
            set { base["Offset"] = value; }
        }
        [ConfigurationProperty("AddressNum", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string AddressNum
        {
            get { return base["AddressNum"] as string; }
            set { base["AddressNum"] = value; }
        }
    }

    public class XrefCollection : ConfigurationElementCollection
    {
        public new Xref this[string name]
        {
            get
            {
                if (IndexOf(name) < 0) return null;
                return (Xref)BaseGet(name);
            }
        }

        public Xref this[int index] => (Xref)BaseGet(index);

        public int IndexOf(string name)
        {
            name = name.ToLower();

            for (int idx = 0; idx < Count; idx++)
            {
                if (this[idx].Name.ToLower() == name)
                    return idx;
            }
            return -1;
        }

        public override ConfigurationElementCollectionType CollectionType => ConfigurationElementCollectionType.BasicMap;

        protected override ConfigurationElement CreateNewElement()
        {
            return new Xref();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Xref)element).Name;
        }

        protected override string ElementName => "Xref";
    }

    public class Xref : ConfigurationElement
    {
        [ConfigurationProperty("Name", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string Name
        {
            get { return base["Name"] as string; }
            set { base["Name"] = value; }
        }
        [ConfigurationProperty("Value", IsRequired = true, IsKey = true, DefaultValue = "")]
        public string Value
        {
            get { return base["Value"] as string; }
            set { base["Value"] = value; }
        }
    }

}
