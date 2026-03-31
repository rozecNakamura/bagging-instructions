export type PreparationWorkGroupKey = {
  delvedt: string;
  majorClassificationCode: string | null;
  middleClassificationCode: string | null;
};

export type PreparationWorkGroup = {
  key: PreparationWorkGroupKey;
  delvedt: string;
  majorClassificationName: string;
  middleClassificationName: string;
  lineCount: number;
};

export type ClassificationOption = {
  id: number;
  code: string;
  name: string;
};
