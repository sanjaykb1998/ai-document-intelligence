export interface Document {
  id: string;
  fileName: string;
  filePath: string;
  uploadedAt: string;
  status: string;
  extractedText?: string | null;
  summary?: string | null;
}