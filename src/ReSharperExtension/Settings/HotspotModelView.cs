﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace ReSharperExtension.Settings
{
    [Serializable]
    public class HotspotModelView : INotifyPropertyChanged, IDataErrorInfo
    {
        private string languageName;
        [XmlElement("LanguageName")]
        public string LanguageName
        {
            get { return languageName; }
            set
            {
                languageName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("LanguageName"));
            }
        }

        private string className;
        [XmlElement("ClassName")]
        public string ClassName
        {
            get { return className; }
            set
            {
                className = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ClassName"));
            }
        }

        private string methodName;
        [XmlElement("MethodName")]
        public string MethodName
        {
            get { return methodName; }
            set
            {
                methodName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("MethodName"));
            }
        }

        private int argumentPos;
        [XmlElement("ArgumentPosition")]
        public int ArgumentPosition
        {
            get
            {
                return argumentPos;
            }
            set
            {
                argumentPos = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ArgumentPosition"));
            }
        }

        private string returnedType;
        [XmlElement("ReturnedType")]
        public string ReturnedType
        {
            get
            {
                return returnedType;
            }
            set
            {
                returnedType = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ReturnedType"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, e);
        }

        public static Hotspot.Hotspot ToHotspot(HotspotModelView modelView)
        {
            return new Hotspot.Hotspot(modelView.LanguageName, modelView.ClassName, modelView.MethodName,
                modelView.ArgumentPosition, modelView.ReturnedType);
        }

        public string this[string columnName]
        {
            get
            {
                if (columnName == "LanguageName")
                {
                    return IsCorrectName(LanguageName)
                            ? null
                            : "Language name must only contain letters, digits, and underlying";

                }
                if (columnName == "ClassName")
                {
                    return IsCorrectName(className)
                            ? null
                            : "Class name must only contain letters, digits, and underlying";
                }
                if (columnName == "MethodName")
                {
                    return IsCorrectName(methodName)
                            ? null
                            : "Method name must only contain letters, digits, and underlying";
                }
                if (columnName == "ArgumentPosition")
                {
                    return argumentPos >= 0
                        ? null
                        : "Argument position must be non-negative number";

                }
                if (columnName == "ReturnedType")
                {
                    return IsCorrectName(returnedType)
                        ? null
                        : "Returned type must only contain letters, digits, and underlying";
                }
                return null;
            }
        }

        public bool AmCorrect()
        {
            var list = new List<string>{languageName, className, methodName, returnedType};
            return list.TrueForAll(IsCorrectName) && argumentPos >= 0;
        }

        private bool IsCorrectName(string input)
        {
            if (Char.IsDigit(input[0]))
                return false;

            foreach (char symbol in input)
            {
                if (!(Char.IsLetterOrDigit(symbol) || symbol == '_'))
                    return false;
            }
            return true;
        }

        [XmlIgnore]
        public string Error { get; private set; }
    }

    [Serializable]
    [XmlRoot("RootCollection")]
    public class HotspotWrapperCollection
    {
        [XmlArray("Collection"), XmlArrayItem("Item", typeof(HotspotModelView))]
        public List<HotspotModelView> Collection { get; set; }
    }
}
