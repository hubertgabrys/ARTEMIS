using System;

namespace USZ_ARTEMIS.Core.StructureCreation
{
    public sealed class CouchReplacementException : Exception
    {
        internal CouchReplacementException(
            string message,
            bool previousCouchAvailable,
            Exception innerException)
            : base(message, innerException)
        {
            PreviousCouchAvailable = previousCouchAvailable;
        }

        public bool PreviousCouchAvailable { get; }
    }

    public static class CouchReplacementTransaction
    {
        public static void Execute(
            Action<Action> replaceCouch,
            Action restorePreviousCouch)
        {
            if (replaceCouch == null)
            {
                throw new ArgumentNullException(nameof(replaceCouch));
            }

            if (restorePreviousCouch == null)
            {
                throw new ArgumentNullException(nameof(restorePreviousCouch));
            }

            bool previousCouchRemoved = false;

            try
            {
                replaceCouch(() => previousCouchRemoved = true);
            }
            catch (Exception replacementException)
            {
                if (!previousCouchRemoved)
                {
                    throw new CouchReplacementException(
                        "Couch repositioning failed. The previous couch remains unchanged.",
                        true,
                        replacementException);
                }

                try
                {
                    restorePreviousCouch();
                }
                catch (Exception restorationException)
                {
                    throw new CouchReplacementException(
                        "Couch repositioning failed and the previous couch could not be restored. " +
                        "The couch structures may be missing; do not save these modifications.",
                        false,
                        new AggregateException(replacementException, restorationException));
                }

                throw new CouchReplacementException(
                    "Couch repositioning failed. The previous couch position was restored.",
                    true,
                    replacementException);
            }
        }
    }
}
