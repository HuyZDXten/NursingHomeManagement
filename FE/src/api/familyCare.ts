import { apiClient } from './client';
import type { Feedback, FeedbackCategory, FeedbackStatus, TimelineItem } from '../types/care';

export interface CreateFeedbackForm {
  residentId?: number;
  category: FeedbackCategory;
  rating?: number;
  subject: string;
  content: string;
}

export interface RespondFeedbackForm {
  status: 'InProgress' | 'Resolved';
  assignedToId?: number;
  response?: string;
}

export const familyCareApi = {
  timeline: (residentId: number, date?: string) =>
    apiClient.get<TimelineItem[]>(`/api/family/residents/${residentId}/timeline`, { params: { date } }).then((r) => r.data),
  myFeedbacks: () =>
    apiClient.get<Feedback[]>('/api/feedbacks/mine').then((r) => r.data),
  createFeedback: (body: CreateFeedbackForm) =>
    apiClient.post<Feedback>('/api/feedbacks', body).then((r) => r.data),
  allFeedbacks: (status?: FeedbackStatus, category?: FeedbackCategory) =>
    apiClient.get<Feedback[]>('/api/feedbacks', { params: { status, category } }).then((r) => r.data),
  respondFeedback: (id: number, body: RespondFeedbackForm) =>
    apiClient.post<Feedback>(`/api/feedbacks/${id}/respond`, body).then((r) => r.data),
};
