using System;
using System.Collections.ObjectModel;

namespace ArgumentParser.Internal;

public interface IArgumentProvider
{
	ReadOnlyCollection<PropertyAndAttributeInfo> GetOptionArguments();
	ReadOnlyCollection<PropertyAndAttributeInfo> GetFlagArguments();
	ReadOnlyCollection<PropertyAndAttributeInfo> GetPositionalArguments();
}
