﻿/********************************************************
*                                                       *
*   Copyright (C) Microsoft. All rights reserved.       *
*                                                       *
********************************************************/

namespace MicrosoftResearch.Infer.Learners.BayesPointMachineClassifierInternal
{
    using MicrosoftResearch.Infer;
    using MicrosoftResearch.Infer.Distributions;
    using MicrosoftResearch.Infer.Models;

    using GammaArray = MicrosoftResearch.Infer.Distributions.DistributionStructArray<Distributions.Gamma, double>;
    using GaussianMatrix = Distributions.DistributionRefArray<Distributions.DistributionStructArray<MicrosoftResearch.Infer.Distributions.Gaussian, double>, double[]>;

    /// <summary>
    /// An Infer.NET model of a multi-class Bayes point machine classifier with factorized weight distributions
    /// and features in a dense representation.
    /// <para>
    /// The factorized weight distributions can be interpreted as a <see cref="VectorGaussian"/> distribution 
    /// with diagonal covariance matrix, which ignores possible correlations between weights.
    /// </para>
    /// </summary>
    internal class DenseMulticlassModel
    {
        #region Fields and constructor

        /// <summary>
        /// The range over instances.
        /// </summary>
        private Range instanceRange;

        /// <summary>
        /// The range over features.
        /// </summary>
        private Range featureRange;

        /// <summary>
        /// The range over classes.
        /// </summary>
        private Range classRange;

        /// <summary>
        /// The variables for the observed feature values.
        /// </summary>
        private VariableArray<VariableArray<double>, double[][]> featureValues;

        /// <summary>
        /// The variables for the shared weights.
        /// </summary>
        private VariableArray<VariableArray<double>, double[][]> sharedWeights;

        /// <summary>
        /// Initializes a new instance of the <see cref="DenseMulticlassModel"/> class.
        /// </summary>
        /// <param name="computeModelEvidence">If true, the model computes evidence.</param>
        /// <param name="useCompoundWeightPriorDistributions">
        /// If true, the model uses compound prior distributions over weights. Otherwise, <see cref="Gaussian"/> prior distributions are used.
        /// </param>
        /// <param name="breakSymmetries">If true, symmetries in the model are broken.</param>
        public DenseMulticlassModel(
            bool computeModelEvidence, 
            bool useCompoundWeightPriorDistributions,
            bool breakSymmetries)
        {
            if (computeModelEvidence)
            {
                // Create Bayes point machine classifier model with surrounding evidence block
                this.ModelSelector = Variable.Bernoulli(0.5).Named("ModelSelector");
                using (Variable.If(this.ModelSelector))
                {
                    this.DefineModel(useCompoundWeightPriorDistributions, breakSymmetries);
                }
            }
            else
            {
                // Create Bayes point machine classifier model without evidence block
                this.DefineModel(useCompoundWeightPriorDistributions, breakSymmetries);
            }
        }

        #endregion  

        #region Variables accessible in inference queries

        /// <summary>
        /// Gets the random variables for the labels.
        /// </summary>
        public VariableArray<int> Labels { get; private set; }

        /// <summary>
        /// Gets the random variables for the weights.
        /// </summary>
        public VariableArray<VariableArray<double>, double[][]> Weights { get; private set; }

        /// <summary>
        /// Gets the random variables for the weight precision rates.
        /// </summary>
        public VariableArray<double> WeightPrecisionRates { get; private set; }

        /// <summary>
        /// Gets the random variable for the model selector.
        /// </summary>
        public Variable<bool> ModelSelector { get; private set; }

        #endregion

        #region Model definition

        /// <summary>
        /// Defines the multi-class Bayes point machine classifier model with factorized weight distributions.
        /// </summary>
        /// <param name="useCompoundWeightPriorDistributions">
        /// If true, the model uses compound prior distributions over weights. Otherwise, <see cref="Gaussian"/> prior distributions are used.
        /// </param>
        /// <param name="breakWeightSymmetries">If true, weight symmetries in the model are broken.</param>
        private void DefineModel(bool useCompoundWeightPriorDistributions, bool breakWeightSymmetries)
        {
            this.DefineRanges();
            this.DefinePrior(useCompoundWeightPriorDistributions);
            this.DefineLikelihood(breakWeightSymmetries);
        }

        /// <summary>
        /// Defines the ranges of plates used in the multi-class Bayes point machine classifier model.
        /// </summary>
        private void DefineRanges()
        {
            // Define the range over instances
            var instanceCount = Variable.Observed(default(int)).Named("InstanceCount");
            this.instanceRange = new Range(instanceCount).Named("InstanceRange");

            // Generate a schedule which is sequential over instances
            this.instanceRange.AddAttribute(new Sequential());

            // Define the range over features
            var featureCount = Variable.Observed(default(int)).Named("FeatureCount");
            this.featureRange = new Range(featureCount).Named("FeatureRange");

            // Define the range over classes
            var classCount = Variable.Observed(default(int)).Named("ClassCount");
            this.classRange = new Range(classCount).Named("ClassRange");
        }

        /// <summary>
        /// Defines the prior distributions used in the multi-class Bayes point machine classifier model.
        /// </summary>
        /// <param name="useCompoundWeightPriorDistributions">
        /// If true, the model uses compound prior distributions over weights. Otherwise, <see cref="Gaussian"/> prior distributions are used.
        /// </param>
        private void DefinePrior(bool useCompoundWeightPriorDistributions)
        {
            // Define the observed feature values
            this.featureValues = Variable.Observed(default(double[][]), this.instanceRange, this.featureRange).Named("FeatureValues");

            // Define the weights
            this.Weights = Variable.Array(Variable.Array<double>(this.featureRange), this.classRange).Named("Weights");

            // Define the prior distributions over weights
            if (useCompoundWeightPriorDistributions)
            {
                this.DefineCompoundWeightPrior();
            }
            else
            {
                this.DefineGaussianWeightPrior();
            }
        }

        /// <summary>
        /// Defines the likelihood of the multi-class Bayes point machine classifier model.
        /// </summary>
        /// <param name="breakWeightSymmetries">If true, weight symmetries in the model are broken.</param>
        private void DefineLikelihood(bool breakWeightSymmetries)
        {
            // Define likelihood
            this.DefineLikelihood();

            if (breakWeightSymmetries)
            {
                // Add symmetry-breaking constraints on weights
                this.DefineSymmetryBreaking();
            }
        }

        /// <summary>
        /// Defines the compound prior distributions over weights used in the multi-class Bayes point machine classifier model.
        /// </summary>
        private void DefineCompoundWeightPrior()
        {
            var weightPrecisionRateRates = Variable.Array<double>(this.featureRange).Named("WeightPrecisionRateRates");
            this.WeightPrecisionRates = Variable.Array<double>(this.featureRange).Named("WeightPrecisionRates");
            var sharedWeightPrecisionRates = Variable.Array<double>(this.featureRange).Named("SharedWeightPrecisionRates");
            var weightPrecisions = Variable.Array<double>(this.featureRange).Named("WeightPrecisions");
            this.sharedWeights = Variable.Array(Variable.Array<double>(this.featureRange), this.classRange).Named("SharedWeights");
            var commonWeightPrecision = Variable.GammaFromShapeAndRate(1, 1).Named("CommonWeightPrecision");

            // Define the observed weight constraints for incremental and batched training
            var weightConstraints = Variable.Observed(default(GaussianMatrix)).Named("WeightConstraints");

            // Define the observed weight precision rate constraints for incremental and batched training
            var weightPrecisionRateConstraints = Variable.Observed(default(GammaArray)).Named("WeightPrecisionRateConstraints");

            // The compound Gamma distributions over weight precisions
            weightPrecisionRateRates[this.featureRange] = Variable.GammaFromShapeAndRate(1.0, 1.0).ForEach(this.featureRange);
            sharedWeightPrecisionRates[this.featureRange] = Variable.GammaFromShapeAndRate(1.0, weightPrecisionRateRates[this.featureRange]);
            weightPrecisions[this.featureRange] = commonWeightPrecision / sharedWeightPrecisionRates[this.featureRange];
            
            // The distributions over weights (the shared weights are required to achieve a sequential schedule with symmetry-breaking)
            this.sharedWeights[this.classRange][this.featureRange] = 
                Variable.GaussianFromMeanAndPrecision(0.0, weightPrecisions[this.featureRange]).ForEach(this.classRange);
            this.sharedWeights.AddAttribute(new DivideMessages(false));
            this.Weights[this.classRange][this.featureRange] = Variable.Copy(this.sharedWeights[this.classRange][this.featureRange]);

            // Constrain weights to their marginals divided by their priors to allow for incremental and batched training
            Variable.ConstrainEqualRandom<double[][], GaussianMatrix>(this.Weights, weightConstraints);

            // By switching to the ReplicateOp_NoDivide operators, weight precision rates are initialized before the computation of 
            // the likelihood. This results in the weights having a range similar to the data, avoiding numerical instabilities.
            sharedWeightPrecisionRates.AddAttribute(new DivideMessages(false));

            // Constrain weight precision rates to their marginals divided by their priors to allow for incremental and batched training
            this.WeightPrecisionRates[this.featureRange] = Variable.Copy(sharedWeightPrecisionRates[this.featureRange]);
            Variable.ConstrainEqualRandom<double[], GammaArray>(this.WeightPrecisionRates, weightPrecisionRateConstraints);

            // Anchor weight precision rates at feature values (required for compound prior distributions)
            this.featureValues[this.instanceRange][this.featureRange] = 
                Variable.GaussianFromMeanAndPrecision(0.0, this.WeightPrecisionRates[this.featureRange]).ForEach(this.instanceRange);
        }

        /// <summary>
        /// Defines the <see cref="Gaussian"/> prior distributions over weights used in the multi-class Bayes point machine classifier model.
        /// </summary>
        private void DefineGaussianWeightPrior()
        {
            this.sharedWeights = Variable.Array(Variable.Array<double>(this.featureRange), this.classRange).Named("SharedWeights");

            // The observed Gaussian prior distributions
            var weightPriors = Variable.Observed(default(GaussianMatrix)).Named("WeightPriors");

            // The observed weight constraints for incremental and batched training
            var weightConstraints = Variable.Observed(default(GaussianMatrix)).Named("WeightConstraints");

            // The distributions over weights
            this.sharedWeights.SetTo(Variable<double[][]>.Random(weightPriors));
            this.sharedWeights.AddAttribute(new DivideMessages(false));
            this.Weights[this.classRange][this.featureRange] = Variable.Copy(this.sharedWeights[this.classRange][this.featureRange]);

            // Constrain weights to their marginals divided by their priors to allow for incremental and batched training
            Variable.ConstrainEqualRandom<double[][], GaussianMatrix>(this.Weights, weightConstraints);
        }

        /// <summary>
        /// Defines the likelihood of the multi-class Bayes point machine classifier model.
        /// </summary>
        private void DefineLikelihood()
        {
            // Define the class labels
            this.Labels = Variable.Array<int>(this.instanceRange).Named("Labels");

            // For all instances...
            using (Variable.ForEach(this.instanceRange))
            {
                // ...compute the class scores of this instance
                VariableArray<double> noisyScores = this.ComputeNoisyScores(this.Weights, this.featureValues[this.instanceRange]);
                this.Labels[this.instanceRange] = Variable.DiscreteUniform(this.classRange);

                // ... and constrain the score of the class of the label to be maximal
                this.ConstrainMaximum(this.Labels[this.instanceRange], noisyScores);
            }
        }

        /// <summary>
        /// Defines the symmetry-breaking constraints. 
        /// For each feature, the sum of weights is constraint to be constant over all classes.
        /// </summary>
        private void DefineSymmetryBreaking()
        {
            var transposedWeights = Variable.Array(Variable.Array<double>(this.classRange), this.featureRange).Named("TransposedWeights");
            var transposedWeightSums = Variable.Array<double>(this.featureRange).Named("TransposedWeightSums");

            // Transpose the weights
            transposedWeights[this.featureRange][this.classRange] = Variable.Copy(this.sharedWeights[this.classRange][this.featureRange]);

            // For each feature, sum the transposed weights over classes
            transposedWeightSums[this.featureRange] = Variable.Sum(transposedWeights[this.featureRange]);

            // Constrain all sums to be constant
            Variable.ConstrainEqual(transposedWeightSums[this.featureRange], 0);
        }

        /// <summary>
        /// Computes the noisy class scores for given feature values and weights.
        /// </summary>
        /// <param name="weights">The weights.</param>
        /// <param name="values">The feature values of a single instance.</param>
        /// <returns>The noisy scores, one for each class.</returns>
        private VariableArray<double> ComputeNoisyScores(VariableArray<VariableArray<double>, double[][]> weights, VariableArray<double> values)
        {
            var featureScores = Variable.Array(Variable.Array<double>(this.featureRange), this.classRange).Named("FeatureScores");
            var scores = Variable.Array<double>(this.classRange).Named("Scores");
            var noisyScores = Variable.Array<double>(this.classRange).Named("NoisyScores");

            featureScores[this.classRange][this.featureRange] = values[this.featureRange] * weights[this.classRange][this.featureRange];
            scores[this.classRange] = Variable.Sum(featureScores[this.classRange]);
            noisyScores[this.classRange] = Variable.GaussianFromMeanAndVariance(scores[this.classRange], 1.0);
            return noisyScores;
        }

        /// <summary>
        /// Creates a set of stochastic constraints for a specified class, 
        /// effectively implementing a multi-class switch.
        /// </summary>
        /// <param name="argmax">The class variable.</param>
        /// <param name="scores">The scores of all classes.</param>
        private void ConstrainMaximum(Variable<int> argmax, VariableArray<double> scores)
        {
            Range classRangeClone = scores.Range.Clone().Named("ClassMaxNoisyScore");
            using (var classBlock = Variable.ForEach(classRangeClone))
            {
                using (Variable.If(argmax == classBlock.Index))
                {
                    this.ConstrainScore(classBlock.Index, scores);
                }
            }
        }

        /// <summary>
        /// Stochastically constrains the score of a specified class to be larger 
        /// than the scores of all other classes.
        /// </summary>
        /// <param name="argmax">The class with maximum score.</param>
        /// <param name="scores">The scores of all classes.</param>
        private void ConstrainScore(Variable<int> argmax, VariableArray<double> scores)
        {
            Variable<double> argMaxScore = Variable.Copy(scores[argmax]).Named("MaxNoisyScore");
            using (var classBlock = Variable.ForEach(scores.Range))
            {
                using (Variable.IfNot(argmax == classBlock.Index))
                {
                    Variable.ConstrainPositive((argMaxScore - scores[classBlock.Index]).Named("NoisyScoreDeltas"));
                }
            }
        }

        #endregion
    }
}