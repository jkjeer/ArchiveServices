using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Enterprise
{
    [DataContract(Namespace = "")]
    public class PlantAttribute
    {
        [DataMember(Order = 10)]
        public string Code { get; set; }
        [DataMember(Order = 20)]
        public string NumericCode { get; set; }
        [DataMember(Order = 30)]
        public int Numeric { get; set; }
        [DataMember(Order = 40)]
        public int TimeOffset { get; set; }

        public PlantAttribute()
        {
        }

        public PlantAttribute(string plantCode)
        {
            NumericCode = ParsePlantCode(plantCode);
            PlantMap plantMap = new PlantMap();
            PlantAttribute plantAttribute = new PlantAttribute();
            plantAttribute = plantMap.GetAttributesByNum(NumericCode);
            Code = plantAttribute.Code;
            Numeric = plantAttribute.Numeric;
            TimeOffset = plantAttribute.TimeOffset;
        }

        public PlantAttribute(string code, string numericCode, int numeric, int timeOffset)
        {
            Code = code;
            NumericCode = numericCode;
            Numeric = numeric;
            TimeOffset = timeOffset;
        }

        public PlantAttribute GetPlantAttributeById(string plantId)
        {
            PlantMap plantMap = new PlantMap();
            return plantMap.Nums[ParsePlantCode(plantId)];
        }

        private string ParsePlantCode(string plantCode)
        {
            try
            {
                string _plantId = plantCode.PadLeft(2, '0').ToUpper();
                switch (_plantId.Length)
                {
                    case 2:
                        NumericCode = _plantId;
                        Code = GetPlantCode(_plantId);
                        break;
                    case 4:
                        Code = _plantId;
                        NumericCode = GetPlantId(_plantId);
                        break;
                }
            }
            catch (Exception e)
            {
                string eMsg = e.Message;
                throw new ApplicationException(plantCode + " does not EXIST in Plant Map");
            }
            return NumericCode;
        }

        private string GetPlantCode(string plantId)
        {
            PlantMap plantMap = new PlantMap();
            string plantCode = plantMap.GetAttributesByNum(plantId).Code;
            return plantCode;
        }

        private string GetPlantId(string plantCode)
        {
            PlantMap plantMap = new PlantMap();
            string plantId = plantMap.GetAttributesByCode(plantCode).NumericCode;
            return plantId;
        }


    }

    public class PlantMap
    {
        public Dictionary<string, PlantAttribute> Codes { get; set; }
        public Dictionary<string, PlantAttribute> Nums { get; set; }

        public PlantMap()
        {
            var codes = new Dictionary<string, PlantAttribute>();
            codes.Add("MPIG", new PlantAttribute("MPIG", "01", 1, 0));
            codes.Add("GFIG", new PlantAttribute("GFIG", "02", 2, 0));
            codes.Add("SGIG", new PlantAttribute("SGIG", "03", 3, 0));
            codes.Add("NFCG", new PlantAttribute("NFCG", "04", 4, 0));
            codes.Add("SGCG", new PlantAttribute("SGCG", "05", 5, 0));
            codes.Add("TOIG", new PlantAttribute("TOIG", "06", 6, 0));
            codes.Add("TOTG", new PlantAttribute("TOTG", "07", 7, 0));
            codes.Add("AMLG", new PlantAttribute("AMLG", "08", 8, 0));
            codes.Add("MNFG", new PlantAttribute("MNFG", "09", 9, 0));
            codes.Add("BFCG", new PlantAttribute("BFCG", "10", 10, 1));
            codes.Add("TWCG", new PlantAttribute("TWCG", "11", 11, -2));
            codes.Add("PTFG", new PlantAttribute("PTFG", "12", 12, 0));
            codes.Add("FMIG", new PlantAttribute("FMIG", "14", 14, 1));
            codes.Add("MVCG", new PlantAttribute("MVCG", "15", 15, -2));
            codes.Add("FRIG", new PlantAttribute("FRIG", "16", 16, 0));
            codes.Add("OCIG", new PlantAttribute("OCIG", "17", 17, 1));
            codes.Add("MRFG", new PlantAttribute("MRFG", "18", 18, 0));
            codes.Add("WXIG", new PlantAttribute("WXIG", "19", 19, 0));
            codes.Add("GTCG", new PlantAttribute("GTCG", "20", 20, -2));
            codes.Add("WXCG", new PlantAttribute("WXCG", "21", 21, 0));
            codes.Add("HRIG", new PlantAttribute("HRIG", "22", 22, -2));
            codes.Add("DRFG", new PlantAttribute("DRFG", "23", 23, 0));
            codes.Add("CGCG", new PlantAttribute("CGCG", "24", 24, -2));
            codes.Add("ROIG", new PlantAttribute("ROIG", "25", 25, 1));
            codes.Add("LLCG", new PlantAttribute("LLCG", "26", 26, -1));
            codes.Add("WLFG", new PlantAttribute("WLFG", "27", 27, -2));
            codes.Add("WBIG", new PlantAttribute("WBIG", "28", 28, 1));
            codes.Add("OCLG", new PlantAttribute("OCLG", "29", 29, 1));
            codes.Add("CHTG", new PlantAttribute("CHTG", "30", 30, -2));
            codes.Add("DRML", new PlantAttribute("DRML", "31", 31, 0));
            codes.Add("MZST", new PlantAttribute("MZST", "32", 32, 0));
            codes.Add("SGSC", new PlantAttribute("SGSC", "33", 33, 0));
            codes.Add("WKIG", new PlantAttribute("WKIG", "34", 34, 0));
            codes.Add("SGAG", new PlantAttribute("SGAG", "35", 35, 0));
            codes.Add("SGRT", new PlantAttribute("SGRT", "36", 36, 0));
            codes.Add("NELG", new PlantAttribute("NELG", "37", 37, 1));
            codes.Add("LACT", new PlantAttribute("LACT", "38", 38, -2));
            codes.Add("ISCT", new PlantAttribute("ISCT", "39", 39, 0));
            codes.Add("MACT", new PlantAttribute("MACT", "41", 41, 1));
            codes.Add("UTCT", new PlantAttribute("UTCT", "42", 42, 1));
            codes.Add("ETCT", new PlantAttribute("ETCT", "43", 43, 1));
            codes.Add("ADCT", new PlantAttribute("ADCT", "44", 44, 1));
            codes.Add("BEIG", new PlantAttribute("BEIG", "45", 45, -1));
            codes.Add("BFIG", new PlantAttribute("BFIG", "77", 77, 0));
            codes.Add("HDDC", new PlantAttribute("HDDC", "92", 92, 0));
            Codes = codes;

            var nums = new Dictionary<string, PlantAttribute>();
            nums.Add("01", new PlantAttribute("MPIG", "01", 1, 0));
            nums.Add("02", new PlantAttribute("GFIG", "02", 2, 0));
            nums.Add("03", new PlantAttribute("SGIG", "03", 3, 0));
            nums.Add("04", new PlantAttribute("NFCG", "04", 4, 0));
            nums.Add("05", new PlantAttribute("SGCG", "05", 5, 0));
            nums.Add("06", new PlantAttribute("TOIG", "06", 6, 0));
            nums.Add("07", new PlantAttribute("TOTG", "07", 7, 0));
            nums.Add("08", new PlantAttribute("AMLG", "08", 8, 0));
            nums.Add("09", new PlantAttribute("MNFG", "09", 9, 0));
            nums.Add("10", new PlantAttribute("BFCG", "10", 10, 1));
            nums.Add("11", new PlantAttribute("TWCG", "11", 11, -2));
            nums.Add("12", new PlantAttribute("PTFG", "12", 12, 0));
            nums.Add("14", new PlantAttribute("FMIG", "14", 14, 1));
            nums.Add("15", new PlantAttribute("MVCG", "15", 15, -2));
            nums.Add("16", new PlantAttribute("FRIG", "16", 16, 0));
            nums.Add("17", new PlantAttribute("OCIG", "17", 17, 1));
            nums.Add("18", new PlantAttribute("MRFG", "18", 18, 0));
            nums.Add("19", new PlantAttribute("WXIG", "19", 19, 0));
            nums.Add("20", new PlantAttribute("GTCG", "20", 20, -2));
            nums.Add("21", new PlantAttribute("WXCG", "21", 21, 0));
            nums.Add("22", new PlantAttribute("HRIG", "22", 22, -2));
            nums.Add("23", new PlantAttribute("DRFG", "23", 23, 0));
            nums.Add("24", new PlantAttribute("CGCG", "24", 24, -2));
            nums.Add("25", new PlantAttribute("ROIG", "25", 25, 1));
            nums.Add("26", new PlantAttribute("LLCG", "26", 26, -1));
            nums.Add("27", new PlantAttribute("WLFG", "27", 27, -2));
            nums.Add("28", new PlantAttribute("WBIG", "28", 28, 1));
            nums.Add("29", new PlantAttribute("OCLG", "29", 29, 1));
            nums.Add("30", new PlantAttribute("CHTG", "30", 30, -2));
            nums.Add("31", new PlantAttribute("DRML", "31", 31, 0));
            nums.Add("32", new PlantAttribute("MZST", "32", 32, 0));
            nums.Add("33", new PlantAttribute("SGSC", "33", 33, 0));
            nums.Add("34", new PlantAttribute("WKIG", "34", 34, 0));
            nums.Add("35", new PlantAttribute("SGAG", "35", 35, 0));
            nums.Add("36", new PlantAttribute("SGRT", "36", 36, 0));
            nums.Add("37", new PlantAttribute("NELG", "37", 37, 1));
            nums.Add("38", new PlantAttribute("LACT", "38", 38, -2));
            nums.Add("39", new PlantAttribute("ISCT", "39", 39, 0));
            nums.Add("41", new PlantAttribute("MACT", "41", 41, 1));
            nums.Add("42", new PlantAttribute("UTCT", "42", 42, 1));
            nums.Add("43", new PlantAttribute("ETCT", "43", 43, 1));
            nums.Add("44", new PlantAttribute("ADCT", "44", 44, 1));
            nums.Add("45", new PlantAttribute("BEIG", "45", 45, -1));
            nums.Add("77", new PlantAttribute("BFIG", "77", 77, 0));
            nums.Add("92", new PlantAttribute("HDDC", "92", 92, 0));
            Nums = nums;
        }

        public PlantAttribute GetAttributebyId(string plantId)
        {
            var pt = new PlantAttribute();
            try
            {
                string _plantId = plantId.PadLeft(2, '0').ToUpper();
                switch (_plantId.Length)
                {
                    case 2:
                        pt = this.Nums[_plantId];
                        break;
                    case 4:
                        pt = this.Codes[_plantId];
                        break;
                }
            }
            catch (Exception e)
            {
                string eMsg = e.Message;
                throw new ApplicationException(plantId + " does not EXIST in Plant Map");
            }
            return pt;
        }

        public PlantAttribute GetAttributesByCode(string plantCode)
        {
            try
            {
                return this.Codes[plantCode];
            }
            catch (Exception e)
            {
                string eMsg = e.Message;
                throw new ApplicationException(plantCode + " does not EXIST in Plant Map");
            }
        }

        public PlantAttribute GetAttributesByNum(string plantNum)
        {
            try
            {
                return this.Nums[plantNum];
            }
            catch (Exception e)
            {
                string eMsg = e.Message;
                throw new ApplicationException(plantNum + " does not EXIST in Plant Map");
            }
        }



    }
}
