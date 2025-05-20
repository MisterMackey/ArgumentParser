using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ArgumentParser.Internal
{
    /// <summary>
    /// Provides argument information by delegating to an underlying AttributeFactory.
    /// </summary>
    public class UserSpecifiedArgumentProvider : IArgumentProvider
    {
        private readonly AttributeFactory _attributeFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="UserSpecifiedArgumentProvider"/> class.
        /// </summary>
        /// <param name="attributeFactory">The <see cref="AttributeFactory"/> used to provide argument information.</param>
        public UserSpecifiedArgumentProvider(AttributeFactory attributeFactory)
        {
            _attributeFactory = attributeFactory ?? throw new ArgumentNullException(nameof(attributeFactory));
        }

        /// <summary>
        /// Gets the option arguments by delegating to the underlying <see cref="AttributeFactory"/>.
        /// </summary>
        /// <returns>A read-only collection of option arguments.</returns>
        public ReadOnlyCollection<PropertyAndAttributeInfo> GetOptionArguments()
        {
            return _attributeFactory.GetOptionAttributes().ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the flag arguments by delegating to the underlying <see cref="AttributeFactory"/>.
        /// </summary>
        /// <returns>A read-only collection of flag arguments.</returns>
        public ReadOnlyCollection<PropertyAndAttributeInfo> GetFlagArguments()
        {
            return _attributeFactory.GetFlagAttributes().ToList().AsReadOnly();
        }

        /// <summary>
        /// Gets the positional arguments by delegating to the underlying <see cref="AttributeFactory"/>.
        /// </summary>
        /// <returns>A read-only collection of positional arguments.</returns>
        public ReadOnlyCollection<PropertyAndAttributeInfo> GetPositionalArguments()
        {
            return _attributeFactory.GetPositionalAttributes().ToList().AsReadOnly();
        }
    }
}
