/********************************************************
*                                                       *
*   Copyright (C) Microsoft. All rights reserved.       *
*                                                       *
********************************************************/

namespace MicrosoftResearch.Infer.Learners.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using MicrosoftResearch.Infer.Collections;
    using MicrosoftResearch.Infer.Learners.Mappings;
    using MicrosoftResearch.Infer.Maths;

    /// <summary>
    /// The classifier mapping.
    /// </summary>
    [Serializable]
    public class ClassifierMapping : IClassifierMapping<IList<LabeledFeatureValues>, LabeledFeatureValues, IList<LabelDistribution>, string, Vector>
    {
        /// <summary>
        /// The bidirectional mapping from feature names to feature indexes.
        /// </summary>
        private readonly IndexedSet<string> featureSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClassifierMapping"/> class.
        /// </summary>
        /// <param name="featureSet">An optional bidirectional mapping from feature names to indexes.</param>
        public ClassifierMapping(IndexedSet<string> featureSet = null)
        {
            this.featureSet = featureSet;
        }

        /// <summary>
        /// Provides the instances for a given instance source.
        /// </summary>
        /// <param name="instanceSource">The source of instances.</param>
        /// <returns>The instances provided by the instance source.</returns>
        /// <remarks>Assumes that the same instance source always provides the same instances.</remarks>
        public IEnumerable<LabeledFeatureValues> GetInstances(IList<LabeledFeatureValues> instanceSource)
        {
            if (instanceSource == null)
            {
                throw new ArgumentNullException("instanceSource");
            }

            return instanceSource;
        }

        /// <summary>
        /// Provides the features for a given instance.
        /// </summary>
        /// <param name="instance">The instance to provide features for.</param>
        /// <param name="instanceSource">An optional source of instances.</param>
        /// <returns>The features for the given instance.</returns>
        /// <remarks>Assumes that the same instance source always provides the same features for a given instance.</remarks>
        public Vector GetFeatures(LabeledFeatureValues instance, IList<LabeledFeatureValues> instanceSource = null)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            return instance.GetSparseFeatureVector(this.featureSet);
        }

        /// <summary>
        /// Provides the label for a given instance.
        /// </summary>
        /// <param name="instance">The instance to provide the label for.</param>
        /// <param name="instanceSource">An optional source of instances.</param>
        /// <param name="labelSource">An optional source of labels.</param>
        /// <returns>The label of the given instance.</returns>
        /// <remarks>Assumes that the same sources always provide the same label for a given instance.</remarks>
        public string GetLabel(LabeledFeatureValues instance, IList<LabeledFeatureValues> instanceSource, IList<LabelDistribution> labelSource = null)
        {
            if (instance == null)
            {
                throw new ArgumentNullException("instance");
            }

            return instance.LabelDistribution.GetMode();
        }

        /// <summary>
        /// Gets all class labels.
        /// </summary>
        /// <param name="instanceSource">An optional instance source.</param>
        /// <param name="labelSource">An optional label source.</param>
        /// <returns>All possible values of a label.</returns>
        public IEnumerable<string> GetClassLabels(IList<LabeledFeatureValues> instanceSource, IList<LabelDistribution> labelSource = null)
        {
            if (instanceSource == null)
            {
                throw new ArgumentNullException("instanceSource");
            }

            return instanceSource.First().LabelDistribution.LabelSet.Elements;
        }
    }
}