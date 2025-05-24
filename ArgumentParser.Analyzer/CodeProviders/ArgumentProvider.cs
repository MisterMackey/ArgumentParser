using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArgumentParser.Internal.Utilities;

namespace ArgumentParser.Internal
{
	/// <summary>
	/// Provides argument information by delegating to an underlying AttributeFactory.
	/// </summary>
	public class ArgumentProvider
	{
		private readonly AttributeFactory _attributeFactory;
		private readonly Configuration _config;
		private List<PropertyAndAttributeInfo> _optionArguments = [];
		private List<PropertyAndAttributeInfo> _flagArguments = [];
		private List<PropertyAndAttributeInfo> _positionalArguments = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="ArgumentProvider"/> class.
		/// </summary>
		/// <param name="attributeFactory">The <see cref="AttributeFactory"/> used to provide argument information.</param>
		/// <param name="config">The <see cref="Configuration"/> object containing configuration settings.</param>
		public ArgumentProvider(AttributeFactory attributeFactory, Configuration config)
		{
			_attributeFactory = attributeFactory ?? throw new ArgumentNullException(nameof(attributeFactory));
			_config = config ?? throw new ArgumentNullException(nameof(config));
			InitializeArguments();
		}

		private void InitializeArguments()
		{
			// add help arg if needed
			if (_config.HelpArgumentShouldBeGenerated())
			{
				_flagArguments.Add(new PropertyAndAttributeInfo
				{
					Attribute = new AttributeInfo(
						_config.HelpArgumentShortName ?? "",
						_config.HelpArgumentLongName ?? "",
						"Display help text",
						false,
						-1
					),
					PropertyName = "DisplayHelp",
					PropertyType = "bool"
				});
			}
			// add the user provided arguments
			_optionArguments.AddRange(_attributeFactory.GetOptionAttributes());
			_flagArguments.AddRange(_attributeFactory.GetFlagAttributes());
			_positionalArguments.AddRange(_attributeFactory.GetPositionalAttributes());
		}

		/// <summary>
		/// Gets the option arguments by delegating to the underlying <see cref="AttributeFactory"/>.
		/// </summary>
		/// <returns>A read-only collection of option arguments.</returns>
		public ReadOnlyCollection<PropertyAndAttributeInfo> GetOptionArguments()
		{
			return _optionArguments.AsReadOnly();
		}

		/// <summary>
		/// Gets the flag arguments by delegating to the underlying <see cref="AttributeFactory"/>.
		/// </summary>
		/// <returns>A read-only collection of flag arguments.</returns>
		public ReadOnlyCollection<PropertyAndAttributeInfo> GetFlagArguments()
		{
			return _flagArguments.AsReadOnly();
		}

		/// <summary>
		/// Gets the positional arguments by delegating to the underlying <see cref="AttributeFactory"/>.
		/// </summary>
		/// <returns>A read-only collection of positional arguments.</returns>
		public ReadOnlyCollection<PropertyAndAttributeInfo> GetPositionalArguments()
		{
			return _positionalArguments.AsReadOnly();
		}
	}
}
