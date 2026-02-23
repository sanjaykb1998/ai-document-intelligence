export interface Document {
  id: number;
  fileName: string;
  filePath: string;
  uploadedAt: string;
  status: string;
  extractedText: string; // Optional property for extracted text
  summary: string;       // Optional property for summary
}