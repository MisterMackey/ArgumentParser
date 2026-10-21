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
		private readonly GeneratorConfiguration _config;
		private List<PropertyAndAttributeInfo> _optionArguments = [];
		private List<PropertyAndAttributeInfo> _flagArguments = [];
		private List<PropertyAndAttributeInfo> _positionalArguments = [];

		/// <summary>
		/// Initializes a new instance of the <see cref="ArgumentProvider"/> class.
		/// </summary>
		/// <param name="attributeFactory">The <see cref="AttributeFactory"/> used to provide argument information.</param>
		/// <param name="config">The <see cref="GeneratorConfiguration"/> object containing configuration settings.</param>
		public ArgumentProvider(AttributeFactory attributeFactory, GeneratorConfiguration config)
		{
			if (attributeFactory is null)
			{
				throw new ArgumentNullException(nameof(attributeFactory));
			}
			_config = config ?? throw new ArgumentNullException(nameof(config));
			InitializeArguments(
				attributeFactory.GetOptionAttributes(),
				attributeFactory.GetFlagAttributes(),
				attributeFactory.GetPositionalAttributes());
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ArgumentProvider"/> class from arguments that were already extracted.
		/// </summary>
		/// <param name="config">The <see cref="GeneratorConfiguration"/> object containing configuration settings.</param>
		/// <param name="options">The option arguments written by the user.</param>
		/// <param name="flags">The flag arguments written by the user.</param>
		/// <param name="positionals">The positional arguments written by the user.</param>
		public ArgumentProvider(
			GeneratorConfiguration config,
			IEnumerable<PropertyAndAttributeInfo> options,
			IEnumerable<PropertyAndAttributeInfo> flags,
			IEnumerable<PropertyAndAttributeInfo> positionals)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			InitializeArguments(
				options ?? throw new ArgumentNullException(nameof(options)),
				flags ?? throw new ArgumentNullException(nameof(flags)),
				positionals ?? throw new ArgumentNullException(nameof(positionals)));
		}

		private void InitializeArguments(
			IEnumerable<PropertyAndAttributeInfo> options,
			IEnumerable<PropertyAndAttributeInfo> flags,
			IEnumerable<PropertyAndAttributeInfo> positionals)
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
			_optionArguments.AddRange(options);
			_flagArguments.AddRange(flags);
			_positionalArguments.AddRange(positionals);
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
